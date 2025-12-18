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

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (deltaTime <= 0) return;

            // 1. Collect all active colliders
            _allColliders.Clear();
            CollectCollidersRecursive(SceneManager.Instance.RootObjects);

            // 2. Update all physics bodies
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

        private void UpdatePhysicsRecursive(System.Collections.ObjectModel.ObservableCollection<GameObject> objects, float deltaTime)
        {
            foreach (var obj in objects)
            {
                if (obj.IsActive)
                {
                    var physicsBody = obj.GetComponent<PhysicsBodyComponent>();
                    if (physicsBody != null && physicsBody.IsEnabled && !physicsBody.IsKinematic)
                    {
                        UpdateBody(physicsBody, obj, deltaTime);
                    }

                    UpdatePhysicsRecursive(obj.Children, deltaTime);
                }
            }
        }

        private void UpdateBody(PhysicsBodyComponent body, GameObject go, float deltaTime)
        {
            // 1. Apply Gravity
            if (body.UseGravity)
            {
                body.Velocity += Gravity * deltaTime;
            }

            // 2. Apply Drag
            if (body.Drag > 0)
            {
                body.Velocity *= (1.0f - MathHelper.Clamp(body.Drag * deltaTime, 0, 1));
            }

            // 3. Simple Integration (Step-by-step to handle collisions better)
            // For now, just integrate and then resolve
            go.Transform.Position += body.Velocity * deltaTime;

            // 4. Collision Detection & Resolution
            var myColliders = go.GetComponents<ColliderComponent>();
            foreach (var myCol in myColliders)
            {
                if (!myCol.IsEnabled) continue;

                foreach (var otherCol in _allColliders)
                {
                    if (otherCol.GameObject == go) continue; // Don't collide with self

                    if (CheckAndResolveCollision(body, myCol, otherCol))
                    {
                        // Collision occurred and was resolved
                    }
                }
            }

            // 5. Integrate Rotation
            if (body.AngularVelocity.LengthSquared() > 0)
            {
                go.Transform.Rotation += body.AngularVelocity * deltaTime;
                if (body.AngularDrag > 0)
                {
                    body.AngularVelocity *= (1.0f - MathHelper.Clamp(body.AngularDrag * deltaTime, 0, 1));
                }
            }
        }

        private bool CheckAndResolveCollision(PhysicsBodyComponent body, ColliderComponent myCol, ColliderComponent otherCol)
        {
            Vector3 collisionNormal;
            float penetrationDepth;

            if (Collide(myCol, otherCol, out collisionNormal, out penetrationDepth))
            {
                // Resolve position (push out)
                myCol.GameObject.Transform.Position += collisionNormal * penetrationDepth;

                // Resolve velocity (remove component along normal)
                float velocityAlongNormal = Vector3.Dot(body.Velocity, collisionNormal);
                
                // Only resolve if moving towards the other object
                if (velocityAlongNormal < 0)
                {
                    // Simple inelastic collision for now (no bounce)
                    body.Velocity -= collisionNormal * velocityAlongNormal;
                }

                return true;
            }

            return false;
        }

        private bool Collide(ColliderComponent a, ColliderComponent b, out Vector3 normal, out float depth)
        {
            normal = Vector3.Zero;
            depth = 0;

            // Box vs Box
            if (a is BoxColliderComponent boxA && b is BoxColliderComponent boxB)
                return BoxVsBox(boxA, boxB, out normal, out depth);
            
            // Sphere vs Sphere
            if (a is SphereColliderComponent sphereA && b is SphereColliderComponent sphereB)
                return SphereVsSphere(sphereA, sphereB, out normal, out depth);

            // Sphere vs Box
            if (a is SphereColliderComponent s1 && b is BoxColliderComponent b1)
                return SphereVsBox(s1, b1, out normal, out depth);
            if (a is BoxColliderComponent b2 && b is SphereColliderComponent s2)
            {
                bool hit = SphereVsBox(s2, b2, out normal, out depth);
                normal = -normal;
                return hit;
            }

            // Capsule support (Simplified: treat as sphere for now to ensure it stops)
            if (a is CapsuleColliderComponent capA)
                return CapsuleVsOther(capA, b, out normal, out depth);
            if (b is CapsuleColliderComponent capB)
            {
                bool hit = CapsuleVsOther(capB, a, out normal, out depth);
                normal = -normal;
                return hit;
            }

            return false;
        }

        private bool CapsuleVsOther(CapsuleColliderComponent cap, ColliderComponent other, out Vector3 normal, out float depth)
        {
            normal = Vector3.Zero;
            depth = 0;

            Vector3 worldCenter = GetWorldCenter(cap);
            Vector3 worldScale = GetWorldScale(cap.GameObject.Transform);
            
            float scaledRadius;
            float halfHeight;
            Vector3 axis;

            switch (cap.Direction)
            {
                case CapsuleDirection.X_Axis:
                    scaledRadius = cap.Radius * Math.Max(worldScale.Y, worldScale.Z);
                    halfHeight = (cap.Height * worldScale.X) * 0.5f;
                    axis = Vector3.UnitX;
                    break;
                case CapsuleDirection.Z_Axis:
                    scaledRadius = cap.Radius * Math.Max(worldScale.X, worldScale.Y);
                    halfHeight = (cap.Height * worldScale.Z) * 0.5f;
                    axis = Vector3.UnitZ;
                    break;
                default: // Y_Axis
                    scaledRadius = cap.Radius * Math.Max(worldScale.X, worldScale.Z);
                    halfHeight = (cap.Height * worldScale.Y) * 0.5f;
                    axis = Vector3.UnitY;
                    break;
            }

            float offset = Math.Max(0, halfHeight - scaledRadius);
            Vector3 topSphereCenter = worldCenter + axis * offset;
            Vector3 bottomSphereCenter = worldCenter - axis * offset;

            // Test both end spheres
            Vector3 n1, n2;
            float d1, d2;

            bool hitTop = CollideSphereManual(topSphereCenter, scaledRadius, other, out n1, out d1);
            bool hitBottom = CollideSphereManual(bottomSphereCenter, scaledRadius, other, out n2, out d2);

            if (hitTop && hitBottom)
            {
                if (d1 > d2) { normal = n1; depth = d1; }
                else { normal = n2; depth = d2; }
                return true;
            }
            if (hitTop) { normal = n1; depth = d1; return true; }
            if (hitBottom) { normal = n2; depth = d2; return true; }

            return false;
        }

        private bool CollideSphereManual(Vector3 center, float radius, ColliderComponent other, out Vector3 normal, out float depth)
        {
            normal = Vector3.Zero;
            depth = 0;

            if (other is SphereColliderComponent s)
            {
                Vector3 otherCenter = GetWorldCenter(s);
                float otherRadius = s.Radius * GetMaxScale(s.GameObject.Transform);
                return SphereVsSphereManual(center, radius, otherCenter, otherRadius, out normal, out depth);
            }
            if (other is BoxColliderComponent b)
            {
                return SphereVsBoxManual(center, radius, b, out normal, out depth);
            }
            return false;
        }

        private bool BoxVsBox(BoxColliderComponent a, BoxColliderComponent b, out Vector3 normal, out float depth)
        {
            normal = Vector3.Zero;
            depth = 0;

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

            return true;
        }

        private bool SphereVsSphere(SphereColliderComponent a, SphereColliderComponent b, out Vector3 normal, out float depth)
        {
            Vector3 posA = GetWorldCenter(a);
            float radA = a.Radius * GetMaxScale(a.GameObject.Transform);
            Vector3 posB = GetWorldCenter(b);
            float radB = b.Radius * GetMaxScale(b.GameObject.Transform);

            return SphereVsSphereManual(posA, radA, posB, radB, out normal, out depth);
        }

        private bool SphereVsSphereManual(Vector3 posA, float radA, Vector3 posB, float radB, out Vector3 normal, out float depth)
        {
            normal = Vector3.Zero;
            depth = 0;

            float radiusSum = radA + radB;
            float distSq = Vector3.DistanceSquared(posA, posB);

            if (distSq > radiusSum * radiusSum) return false;

            float dist = (float)Math.Sqrt(distSq);
            if (dist > 0)
            {
                normal = (posA - posB) / dist;
                depth = radiusSum - dist;
            }
            else
            {
                normal = Vector3.UnitY;
                depth = radiusSum;
            }

            return true;
        }

        private bool SphereVsBox(SphereColliderComponent s, BoxColliderComponent b, out Vector3 normal, out float depth)
        {
            Vector3 spherePos = GetWorldCenter(s);
            float scaledRadius = s.Radius * GetMaxScale(s.GameObject.Transform);
            return SphereVsBoxManual(spherePos, scaledRadius, b, out normal, out depth);
        }

        private bool SphereVsBoxManual(Vector3 spherePos, float radius, BoxColliderComponent b, out Vector3 normal, out float depth)
        {
            normal = Vector3.Zero;
            depth = 0;

            var box = GetWorldAABB(b);

            // Closest point on AABB to sphere center
            Vector3 closestPoint = Vector3.Clamp(spherePos, box.Min, box.Max);
            
            float distSq = Vector3.DistanceSquared(spherePos, closestPoint);
            if (distSq > radius * radius) return false;

            float dist = (float)Math.Sqrt(distSq);
            if (dist > 0)
            {
                normal = (spherePos - closestPoint) / dist;
                depth = radius - dist;
            }
            else
            {
                // Sphere center is inside the box
                float dl = spherePos.X - box.Min.X;
                float dr = box.Max.X - spherePos.X;
                float db = spherePos.Y - box.Min.Y;
                float dt = box.Max.Y - spherePos.Y;
                float df = spherePos.Z - box.Min.Z;
                float dk = box.Max.Z - spherePos.Z;

                float min = Math.Min(dl, Math.Min(dr, Math.Min(db, Math.Min(dt, Math.Min(df, dk)))));
                depth = radius + min;

                if (min == dl) normal = -Vector3.UnitX;
                else if (min == dr) normal = Vector3.UnitX;
                else if (min == db) normal = -Vector3.UnitY;
                else if (min == dt) normal = Vector3.UnitY;
                else if (min == df) normal = -Vector3.UnitZ;
                else normal = Vector3.UnitZ;
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
