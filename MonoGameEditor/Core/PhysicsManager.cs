using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGameEditor.Core.Components;

namespace MonoGameEditor.Core
{
    public class PhysicsManager
    {
        private static PhysicsManager? _instance;
        public static PhysicsManager Instance => _instance ??= new PhysicsManager();

        public Vector3 Gravity { get; set; } = new Vector3(0, -9.81f, 0);

        private List<ColliderComponent> _allColliders = new List<ColliderComponent>();
        private int _updateCount = 0;

        public void Update(GameTime gameTime)
        {
            _updateCount++;
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (deltaTime <= 0) return;

            // 1. Collect all active colliders
            _allColliders.Clear();
            CollectCollidersRecursive(SceneManager.Instance.RootObjects);

            // Diagnostic every 60 frames
            if (_updateCount % 60 == 0)
            {
                Logger.Log($"[PhysicsManager] Update #{_updateCount}: {_allColliders.Count} colliders found");
            }

            // 2. Update all physics bodies
            int physicsBodyCount = 0;
            foreach (var root in SceneManager.Instance.RootObjects)
            {
                physicsBodyCount += CountPhysicsBodiesRecursive(root);
            }
            
            if (_updateCount % 60 == 0 && physicsBodyCount > 0)
            {
                Logger.Log($"[PhysicsManager] Found {physicsBodyCount} physics bodies");
            }
            UpdatePhysicsRecursive(SceneManager.Instance.RootObjects, deltaTime);
        }

        private void CollectCollidersRecursive(System.Collections.ObjectModel.ObservableCollection<GameObject> objects)
        {
            foreach (var obj in objects)
            {
                if (obj.IsActive)
                {
                    var colliders = obj.GetComponents<ColliderComponent>();
                    foreach (var col in colliders)
                    {
                        if (col.IsEnabled) _allColliders.Add(col);
                    }
                    CollectCollidersRecursive(obj.Children);
                }
            }
        }
        
        private int CountPhysicsBodiesRecursive(GameObject obj)
        {
            int count = 0;
            if (obj.IsActive)
            {
                var body = obj.GetComponent<PhysicsBodyComponent>();
                if (body != null && body.IsEnabled) count++;
                
                foreach (var child in obj.Children)
                {
                    count += CountPhysicsBodiesRecursive(child);
                }
            }
            return count;
        }
        private void UpdatePhysicsRecursive(System.Collections.ObjectModel.ObservableCollection<GameObject> objects, float deltaTime)
        {
            int updatedCount = 0;
            foreach (var obj in objects)
            {
                if (obj.IsActive)
                {
                    var physicsBody = obj.GetComponent<PhysicsBodyComponent>();
                    if (physicsBody != null && physicsBody.IsEnabled && !physicsBody.IsKinematic)
                    {
                        UpdateBody(physicsBody, obj, deltaTime);
                        updatedCount++;
                    }

                    UpdatePhysicsRecursive(obj.Children, deltaTime);
                }
            }
            
            // Diagnostic: Log how many bodies were updated in first few frames
            if (_updateCount <= 5 && updatedCount > 0)
            {
                Logger.Log($"[PhysicsManager] Updated {updatedCount} physics bodies this frame");
            }
        }

        private void UpdateBody(PhysicsBodyComponent body, GameObject go, float deltaTime)
        {
            // Diagnostic: Log physics body state for first 5 updates
            if (_updateCount <= 5 && go.Name.Contains("tripo"))
            {
                string parentName = go.Parent?.Name ?? "ROOT";
                Logger.Log($"[PhysicsManager] {go.Name} (Parent: {parentName}): Pos={go.Transform.Position}, LocalPos={go.Transform.LocalPosition}, UseGravity={body.UseGravity}, Velocity={body.Velocity}");
            }
            
            // 0. Pre-integration: Apply constraints to current velocity
            ApplyVelocityConstraints(body);

            // 1. Apply Forces (Gravity & Drag)
            if (body.UseGravity)
            {
                body.Velocity += Gravity * deltaTime;
                
                // Diagnostic: Log velocity AFTER applying gravity
                if (_updateCount <= 5 && go.Name.Contains("tripo"))
                {
                    Logger.Log($"[PhysicsManager] {go.Name} AFTER gravity: Velocity={body.Velocity}, Pos={go.Transform.Position}");
                }
            }
            if (body.Drag > 0)
            {
                body.Velocity *= (1.0f - MathHelper.Clamp(body.Drag * deltaTime, 0, 1));
            }
            if (body.AngularDrag > 0)
            {
                body.AngularVelocity *= (1.0f - MathHelper.Clamp(body.AngularDrag * deltaTime, 0, 1));
            }

            // 2. Collision Detection: Collect ALL contacts
            List<(Vector3 normal, float depth, Vector3 point, ColliderComponent other, ColliderComponent myCol)> contacts = new List<(Vector3, float, Vector3, ColliderComponent, ColliderComponent)>();
            var myColliders = go.GetComponents<ColliderComponent>();
            foreach (var myCol in myColliders)
            {
                if (!myCol.IsEnabled) continue;
                foreach (var otherCol in _allColliders)
                {
                    if (otherCol.GameObject == go) continue;

                    Vector3 n, c;
                    float d;
                    if (myCol is CapsuleColliderComponent cap)
                    {
                        // Special multi-point detection for capsules
                        CollectCapsuleContacts(contacts, body, cap, otherCol);
                    }
                    else if (Collide(myCol, otherCol, out n, out d, out c))
                    {
                        contacts.Add((n, d, c, otherCol, myCol));
                    }
                }
            }

            // 3. Iterative Velocity Solver (Sequential Impulses)
            // Running multiple iterations makes the physics "solid"
            int velocityIterations = 4;
            for (int i = 0; i < velocityIterations; i++)
            {
                foreach (var ct in contacts)
                {
                    SolveVelocity(body, ct.myCol, ct.normal, ct.point);
                }
            }

            // 4. Integrate Velocity -> Position
            ApplyVelocityConstraints(body);
            go.Transform.Position += body.Velocity * deltaTime;

            // 5. Integrate Rotation
            if (body.AngularVelocity.LengthSquared() > 0.0001f)
            {
                Vector3 degAngularVel = new Vector3(
                    MathHelper.ToDegrees(body.AngularVelocity.X),
                    MathHelper.ToDegrees(body.AngularVelocity.Y),
                    MathHelper.ToDegrees(body.AngularVelocity.Z)
                );
                go.Transform.Rotation += degAngularVel * deltaTime;
            }

            // 6. Iterative Position Solver (De-penetration)
            // We solve position AFTER integration to ensure NO jitter
            int positionIterations = 2;
            for (int i = 0; i < positionIterations; i++)
            {
                foreach (var ct in contacts)
                {
                    // Re-calculate depth after position change
                    Vector3 n, c;
                    float d;
                    if (Collide(ct.myCol, ct.other, out n, out d, out c))
                    {
                        SolvePosition(body, ct.myCol, n, d);
                    }
                }
            }

            // Final Safety
            ApplyVelocityConstraints(body);
            if (body.Velocity.LengthSquared() < 0.0001f) body.Velocity = Vector3.Zero;
            if (body.AngularVelocity.LengthSquared() < 0.0001f) body.AngularVelocity = Vector3.Zero;
        }

        private void ApplyVelocityConstraints(PhysicsBodyComponent body)
        {
            Vector3 v = body.Velocity;
            if (body.FreezePositionX) v.X = 0;
            if (body.FreezePositionY) v.Y = 0;
            if (body.FreezePositionZ) v.Z = 0;
            body.Velocity = v;

            Vector3 av = body.AngularVelocity;
            if (body.FreezeRotationX) av.X = 0;
            if (body.FreezeRotationY) av.Y = 0;
            if (body.FreezeRotationZ) av.Z = 0;
            body.AngularVelocity = av;
        }

        private void CollectCapsuleContacts(List<(Vector3, float, Vector3, ColliderComponent, ColliderComponent)> contacts, PhysicsBodyComponent body, CapsuleColliderComponent cap, ColliderComponent other)
        {
            Vector3 worldCenter = GetWorldCenter(cap);
            Vector3 worldScale = GetWorldScale(cap.GameObject.Transform);
            float scaledRadius, halfHeight;
            Vector3 axis;

            switch (cap.Direction)
            {
                case CapsuleDirection.X_Axis:
                    scaledRadius = cap.Radius * Math.Max(worldScale.Y, worldScale.Z);
                    halfHeight = (cap.Height * worldScale.X) * 0.5f;
                    axis = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX, cap.GameObject.Transform.WorldMatrix));
                    break;
                case CapsuleDirection.Z_Axis:
                    scaledRadius = cap.Radius * Math.Max(worldScale.X, worldScale.Y);
                    halfHeight = (cap.Height * worldScale.Z) * 0.5f;
                    axis = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, cap.GameObject.Transform.WorldMatrix));
                    break;
                default: // Y_Axis
                    scaledRadius = cap.Radius * Math.Max(worldScale.X, worldScale.Z);
                    halfHeight = (cap.Height * worldScale.Y) * 0.5f;
                    axis = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, cap.GameObject.Transform.WorldMatrix));
                    break;
            }

            float segmentHalfLength = Math.Max(0.0f, (halfHeight - scaledRadius));
            Vector3 pA = worldCenter - axis * segmentHalfLength;
            Vector3 pB = worldCenter + axis * segmentHalfLength;
            Vector3[] points = { pA, pB, worldCenter };

            foreach (var p in points)
            {
                Vector3 n, c;
                float d;
                if (CollideSphereManual(p, scaledRadius, other, out n, out d, out c))
                {
                    contacts.Add((n, d, c, other, cap));
                }
            }
        }

        private void SolveVelocity(PhysicsBodyComponent body, ColliderComponent myCol, Vector3 normal, Vector3 contactPoint)
        {
            Vector3 worldBodyCenter = myCol.GameObject.Transform.Position;
            Vector3 r = contactPoint - worldBodyCenter;
            Vector3 vContact = body.Velocity + Vector3.Cross(body.AngularVelocity, r);
            float vn = Vector3.Dot(vContact, normal);

            if (vn >= 0) return; // Already moving away

            // Friction and Impulse math
            float invMass = 1.0f / body.Mass;
            float radius = (myCol is BoxColliderComponent b) ? b.Size.Length() * 0.5f : 0.5f;
            float invI = 1.0f / (body.Mass * radius * radius * 0.4f);

            // Effective Inverse Mass per axis
            Vector3 effInvM = new Vector3(body.FreezePositionX ? 0 : invMass, body.FreezePositionY ? 0 : invMass, body.FreezePositionZ ? 0 : invMass);
            Vector3 effInvI = new Vector3(body.FreezeRotationX ? 0 : invI, body.FreezeRotationY ? 0 : invI, body.FreezeRotationZ ? 0 : invI);

            // 1. Normal Impulse
            Vector3 rcn = Vector3.Cross(r, normal);
            float kNormal = Vector3.Dot(normal * normal, effInvM) + (rcn.X * rcn.X * effInvI.X + rcn.Y * rcn.Y * effInvI.Y + rcn.Z * rcn.Z * effInvI.Z);
            float jn = -vn / (kNormal + 0.0001f);
            
            Vector3 impulse = normal * jn;
            body.Velocity += impulse * effInvM;
            Vector3 angImp = Vector3.Cross(r, impulse);
            body.AngularVelocity += new Vector3(angImp.X * effInvI.X, angImp.Y * effInvI.Y, angImp.Z * effInvI.Z);

            // 2. Friction
            Vector3 vTangent = vContact - (Vector3.Dot(vContact, normal) * normal);
            if (vTangent.LengthSquared() > 0.0001f)
            {
                Vector3 tangent = Vector3.Normalize(vTangent);
                float vt = Vector3.Dot(vContact, tangent);
                Vector3 rct = Vector3.Cross(r, tangent);
                float kTangent = Vector3.Dot(tangent * tangent, effInvM) + (rct.X * rct.X * effInvI.X + rct.Y * rct.Y * effInvI.Y + rct.Z * rct.Z * effInvI.Z);
                float jt = -vt / (kTangent + 0.0001f);
                
                float maxFriction = jn * 0.8f;
                jt = MathHelper.Clamp(jt, -maxFriction, maxFriction);
                
                Vector3 fImpulse = tangent * jt;
                body.Velocity += fImpulse * effInvM;
                Vector3 fAngImp = Vector3.Cross(r, fImpulse);
                body.AngularVelocity += new Vector3(fAngImp.X * effInvI.X, fAngImp.Y * effInvI.Y, fAngImp.Z * effInvI.Z);
            }
        }

        private void SolvePosition(PhysicsBodyComponent body, ColliderComponent myCol, Vector3 normal, float depth)
        {
            const float slop = 0.01f;
            const float bias = 0.3f;
            if (depth <= slop) return;

            Vector3 correction = normal * (depth - slop) * bias;
            if (body.FreezePositionX) correction.X = 0;
            if (body.FreezePositionY) correction.Y = 0;
            if (body.FreezePositionZ) correction.Z = 0;
            
            myCol.GameObject.Transform.Position += correction;
        }

        private bool Collide(ColliderComponent a, ColliderComponent b, out Vector3 normal, out float depth, out Vector3 contactPoint)
        {
            normal = Vector3.Zero;
            depth = 0;
            contactPoint = Vector3.Zero;

            // Box vs Box
            if (a is BoxColliderComponent boxA && b is BoxColliderComponent boxB)
                return BoxVsBox(boxA, boxB, out normal, out depth, out contactPoint);
            
            // Sphere vs Sphere
            if (a is SphereColliderComponent sphereA && b is SphereColliderComponent sphereB)
                return SphereVsSphere(sphereA, sphereB, out normal, out depth, out contactPoint);

            // Sphere vs Box
            if (a is SphereColliderComponent s1 && b is BoxColliderComponent b1)
                return SphereVsBox(s1, b1, out normal, out depth, out contactPoint);
            if (a is BoxColliderComponent b2 && b is SphereColliderComponent s2)
            {
                bool hit = SphereVsBox(s2, b2, out normal, out depth, out contactPoint);
                normal = -normal;
                return hit;
            }

            // Capsule Support
            if (a is CapsuleColliderComponent capA)
                return CapsuleVsOther(capA, b, out normal, out depth, out contactPoint);
            if (b is CapsuleColliderComponent capB)
            {
                bool hit = CapsuleVsOther(capB, a, out normal, out depth, out contactPoint);
                normal = -normal;
                return hit;
            }

            return false;
        }

        private bool CapsuleVsOther(CapsuleColliderComponent cap, ColliderComponent other, out Vector3 normal, out float depth, out Vector3 contactPoint)
        {
            normal = Vector3.Zero;
            depth = 0;
            contactPoint = Vector3.Zero;

            Vector3 worldCenter = GetWorldCenter(cap);
            Vector3 worldScale = GetWorldScale(cap.GameObject.Transform);
            float scaledRadius, halfHeight;
            Vector3 axis;
            
            switch (cap.Direction)
            {
                case CapsuleDirection.X_Axis:
                    scaledRadius = cap.Radius * Math.Max(worldScale.Y, worldScale.Z);
                    halfHeight = (cap.Height * worldScale.X) * 0.5f;
                    axis = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX, cap.GameObject.Transform.WorldMatrix));
                    break;
                case CapsuleDirection.Z_Axis:
                    scaledRadius = cap.Radius * Math.Max(worldScale.X, worldScale.Y);
                    halfHeight = (cap.Height * worldScale.Z) * 0.5f;
                    axis = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, cap.GameObject.Transform.WorldMatrix));
                    break;
                default: // Y_Axis
                    scaledRadius = cap.Radius * Math.Max(worldScale.X, worldScale.Z);
                    halfHeight = (cap.Height * worldScale.Y) * 0.5f;
                    axis = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, cap.GameObject.Transform.WorldMatrix));
                    break;
            }

            float segmentHalfLength = Math.Max(0.0f, (halfHeight - scaledRadius));
            Vector3 pA = worldCenter - axis * segmentHalfLength;
            Vector3 pB = worldCenter + axis * segmentHalfLength;

            // Simple Sphere check for single point queries
            return CollideSphereManual(worldCenter, scaledRadius, other, out normal, out depth, out contactPoint);
        }

        private bool CollideSphereManual(Vector3 center, float radius, ColliderComponent other, out Vector3 normal, out float depth, out Vector3 contactPoint)
        {
            normal = Vector3.Zero;
            depth = 0;
            contactPoint = Vector3.Zero;

            if (other is SphereColliderComponent s)
            {
                Vector3 otherCenter = GetWorldCenter(s);
                float otherRadius = s.Radius * GetMaxScale(s.GameObject.Transform);
                return SphereVsSphereManual(center, radius, otherCenter, otherRadius, out normal, out depth, out contactPoint);
            }
            if (other is BoxColliderComponent b)
            {
                return SphereVsBoxManual(center, radius, b, out normal, out depth, out contactPoint);
            }
            return false;
        }

        private bool BoxVsBox(BoxColliderComponent a, BoxColliderComponent b, out Vector3 normal, out float depth, out Vector3 contactPoint)
        {
            normal = Vector3.Zero;
            depth = 0;
            contactPoint = Vector3.Zero;

            var boxA = GetWorldAABB(a);
            var boxB = GetWorldAABB(b);

            if (!boxA.Intersects(boxB)) return false;

            float overlapX = Math.Min(boxA.Max.X, boxB.Max.X) - Math.Max(boxA.Min.X, boxB.Min.X);
            float overlapY = Math.Min(boxA.Max.Y, boxB.Max.Y) - Math.Max(boxA.Min.Y, boxB.Min.Y);
            float overlapZ = Math.Min(boxA.Max.Z, boxB.Max.Z) - Math.Max(boxA.Min.Z, boxB.Min.Z);

            if (overlapX < overlapY && overlapX < overlapZ)
            {
                depth = overlapX;
                normal = (boxA.Min.X + boxA.Max.X < boxB.Min.X + boxB.Max.X) ? -Vector3.UnitX : Vector3.UnitX;
            }
            else if (overlapY < overlapX && overlapY < overlapZ)
            {
                depth = overlapY;
                normal = (boxA.Min.Y + boxA.Max.Y < boxB.Min.Y + boxB.Max.Y) ? -Vector3.UnitY : Vector3.UnitY;
            }
            else
            {
                depth = overlapZ;
                normal = (boxA.Min.Z + boxA.Max.Z < boxB.Min.Z + boxB.Max.Z) ? -Vector3.UnitZ : Vector3.UnitZ;
            }

            // Contact point = midpoint of overlap
            contactPoint = new Vector3(
                (Math.Max(boxA.Min.X, boxB.Min.X) + Math.Min(boxA.Max.X, boxB.Max.X)) * 0.5f,
                (Math.Max(boxA.Min.Y, boxB.Min.Y) + Math.Min(boxA.Max.Y, boxB.Max.Y)) * 0.5f,
                (Math.Max(boxA.Min.Z, boxB.Min.Z) + Math.Min(boxA.Max.Z, boxB.Max.Z)) * 0.5f
            );

            return true;
        }

        private bool SphereVsSphere(SphereColliderComponent a, SphereColliderComponent b, out Vector3 normal, out float depth, out Vector3 contactPoint)
        {
            Vector3 posA = GetWorldCenter(a);
            float radA = a.Radius * GetMaxScale(a.GameObject.Transform);
            Vector3 posB = GetWorldCenter(b);
            float radB = b.Radius * GetMaxScale(b.GameObject.Transform);

            return SphereVsSphereManual(posA, radA, posB, radB, out normal, out depth, out contactPoint);
        }

        private bool SphereVsSphereManual(Vector3 posA, float radA, Vector3 posB, float radB, out Vector3 normal, out float depth, out Vector3 contactPoint)
        {
            normal = Vector3.Zero;
            depth = 0;
            contactPoint = Vector3.Zero;

            float radiusSum = radA + radB;
            float distSq = Vector3.DistanceSquared(posA, posB);

            if (distSq > radiusSum * radiusSum) return false;

            float dist = (float)Math.Sqrt(distSq);
            if (dist > 0)
            {
                normal = (posA - posB) / dist;
                depth = radiusSum - dist;
                contactPoint = posA - normal * radA;
            }
            else
            {
                normal = Vector3.UnitY;
                depth = radiusSum;
                contactPoint = posA;
            }

            return true;
        }

        private bool SphereVsBox(SphereColliderComponent s, BoxColliderComponent b, out Vector3 normal, out float depth, out Vector3 contactPoint)
        {
            Vector3 spherePos = GetWorldCenter(s);
            float scaledRadius = s.Radius * GetMaxScale(s.GameObject.Transform);
            return SphereVsBoxManual(spherePos, scaledRadius, b, out normal, out depth, out contactPoint);
        }

        private bool SphereVsBoxManual(Vector3 spherePos, float radius, BoxColliderComponent b, out Vector3 normal, out float depth, out Vector3 contactPoint)
        {
            normal = Vector3.Zero;
            depth = 0;
            contactPoint = Vector3.Zero;

            // Transform sphere center to box local space
            Matrix boxWorld = b.GameObject.Transform.WorldMatrix;
            Matrix invBoxWorld = Matrix.Invert(boxWorld);
            Vector3 localSpherePos = Vector3.Transform(spherePos, invBoxWorld);

            // Box is AABB in its own local space
            Vector3 localBoxMin = b.Center - b.Size * 0.5f;
            Vector3 localBoxMax = b.Center + b.Size * 0.5f;

            // Closest point on local AABB to local sphere center
            Vector3 localClosestPoint = Vector3.Clamp(localSpherePos, localBoxMin, localBoxMax);
            
            // Transform closest point back to world space to get real world distance
            Vector3 worldClosestPoint = Vector3.Transform(localClosestPoint, boxWorld);
            
            float distSq = Vector3.DistanceSquared(spherePos, worldClosestPoint);
            if (distSq > radius * radius) return false;

            float dist = (float)Math.Sqrt(distSq);
            if (dist > 0.0001f)
            {
                normal = (spherePos - worldClosestPoint) / dist;
                depth = radius - dist;
                contactPoint = worldClosestPoint;
            }
            else
            {
                // Sphere center is inside the box locally
                float dl = localSpherePos.X - localBoxMin.X;
                float dr = localBoxMax.X - localSpherePos.X;
                float db = localSpherePos.Y - localBoxMin.Y;
                float dt = localBoxMax.Y - localSpherePos.Y;
                float df = localSpherePos.Z - localBoxMin.Z;
                float dk = localBoxMax.Z - localSpherePos.Z;

                float min = Math.Min(dl, Math.Min(dr, Math.Min(db, Math.Min(dt, Math.Min(df, dk)))));
                
                // Get local normal
                Vector3 localNormal;
                if (min == dl) localNormal = -Vector3.UnitX;
                else if (min == dr) localNormal = Vector3.UnitX;
                else if (min == db) localNormal = -Vector3.UnitY;
                else if (min == dt) localNormal = Vector3.UnitY;
                else if (min == df) localNormal = -Vector3.UnitZ;
                else localNormal = Vector3.UnitZ;

                // Transform normal back to world space
                normal = Vector3.Normalize(Vector3.TransformNormal(localNormal, boxWorld));
                depth = radius + min; // Note: this depth is slightly approximated in world space if scale is non-uniform
                contactPoint = spherePos - normal * radius;
            }

            return true;
        }

        private BoundingBox GetWorldAABB(BoxColliderComponent col)
        {
            Vector3 worldCenter = GetWorldCenter(col);
            Vector3 worldScale = GetWorldScale(col.GameObject.Transform);
            Vector3 size = col.Size * worldScale;
            return new BoundingBox(worldCenter - size * 0.5f, worldCenter + size * 0.5f);
        }

        private Vector3 GetWorldCenter(ColliderComponent col)
        {
            // Transform the local center by the world matrix
            return Vector3.Transform(col.Center, col.GameObject.Transform.WorldMatrix);
        }

        private Vector3 GetWorldScale(Transform t)
        {
            t.WorldMatrix.Decompose(out Vector3 scale, out Quaternion _, out Vector3 _);
            return scale;
        }

        private float GetMaxScale(Transform t)
        {
            Vector3 s = GetWorldScale(t);
            return Math.Max(s.X, Math.Max(s.Y, s.Z));
        }

        public void ResetPhysics()
        {
            ResetPhysicsRecursive(SceneManager.Instance.RootObjects);
        }

        private void ResetPhysicsRecursive(System.Collections.ObjectModel.ObservableCollection<GameObject> objects)
        {
            foreach (var obj in objects)
            {
                var body = obj.GetComponent<PhysicsBodyComponent>();
                if (body != null)
                {
                    body.Velocity = Vector3.Zero;
                    body.AngularVelocity = Vector3.Zero;
                }
                ResetPhysicsRecursive(obj.Children);
            }
        }
    }
}
