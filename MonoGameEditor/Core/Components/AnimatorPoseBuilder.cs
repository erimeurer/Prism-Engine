using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MonoGameEditor.Core.Assets;

namespace MonoGameEditor.Core.Components
{
    public struct PoseDebugOptions
    {
        public Vector3 ArmOffset;
        public Vector3 LegOffset;
        public Vector3 RootOffset;
    }

    public static class AnimationPoseBuilder
    {
        // Debug flags
        private static bool _debugLogged = false;
        private static int _logCounter = 0;

        public static void BuildFinalPose(
            ModelData model,
            AnimationClip clip,
            float time,
            Matrix4x4[] finalMatrices,
            List<GameObject> boneObjects,
            PoseDebugOptions? debugOptions = null)
        {
            var animated = clip.GetTransformsAtTime(time);
            
            // Log first frame only
            if (!_debugLogged)
            {
                MonoGameEditor.Core.Logger.Log($"[AnimatorPoseBuilder] START FRAME. Bones: {model.Bones.Count}, AnimKeys: {animated.Count}");
                foreach (var k in animated.Keys.Take(5)) MonoGameEditor.Core.Logger.Log($"   Key: {k}");
            }

            var globals = new Matrix4x4[model.Bones.Count];
            var computed = new bool[model.Bones.Count];

            for (int i = 0; i < model.Bones.Count; i++)
                ComputeBone(i, model, animated, globals, computed, boneObjects, debugOptions);
            
            // DEBUG: Dump if limbs are missing
            if (!_debugLogged)
            {
                var missingBones = model.Bones.Where(b => !TryGetAnimatedTransform(animated, b.Name, out _)).Select(b => b.Name).ToList();
                var availableKeys = animated.Keys.ToList();
                
                if (missingBones.Count > 0)
                {
                    MonoGameEditor.Core.Logger.LogWarning($"[AnimatorPoseBuilder] Mismatched Bones ({missingBones.Count}): {string.Join(", ", missingBones.Take(5))}...");
                    MonoGameEditor.Core.Logger.LogWarning($"[AnimatorPoseBuilder] Available Keys ({availableKeys.Count}): {string.Join(", ", availableKeys.Take(10))}... [See Output for full list]");
                    
                     // Dump ALL keys if small enough
                     if (availableKeys.Count < 100)
                         foreach(var k in availableKeys) MonoGameEditor.Core.Logger.Log($"   Key: '{k}'");
                }
            }

            if (!_debugLogged) _debugLogged = true;

            for (int i = 0; i < model.Bones.Count; i++)
            {
                finalMatrices[i] = model.Bones[i].OffsetMatrix * globals[i];
            }
        }

        public static void BuildFinalPose(
            ModelData model,
            AnimationClip clip,
            float time,
            Matrix4x4[] finalMatrices)
        {
            BuildFinalPose(model, clip, time, finalMatrices, null);
        }

        // Blended pose omitted for brevity/clarity in this fix phase, assuming BuildFinalPose is main driver.
        // (You can copy the same ComputeBone logic to Blended if needed later)
        public static void BuildBlendedPose(
             ModelData model,
             AnimationClip from,
             AnimationClip to,
             float timeFrom,
             float timeTo,
             float blend,
             Matrix4x4[] finalMatrices, 
             PoseDebugOptions? debugOptions = null)
        {
             BuildFinalPose(model, from, timeFrom, finalMatrices, null, debugOptions);
        }

        private static Matrix4x4 ComputeBone(
            int index,
            ModelData model,
            Dictionary<string, (Vector3 pos, Quaternion rot, Vector3 scale)> animated,
            Matrix4x4[] globals,
            bool[] computed,
            List<GameObject> boneObjects,
            PoseDebugOptions? debugOptions)
        {
            if (computed[index])
                return globals[index];

            var bone = model.Bones[index];
            (Vector3 pos, Quaternion rot, Vector3 scale) t;

            // 1. Get Initial Bind Pose (Safe fallback)
            Matrix4x4.Decompose(bone.LocalTransform, out t.scale, out t.rot, out t.pos);

            // 2. Apply Animation with Smart Lock
            bool foundAnim = TryGetAnimatedTransform(animated, bone.Name, out var animT);
            
            if (!_debugLogged && index < 10) // Log first 10 bones status
            {
                MonoGameEditor.Core.Logger.Log($"   Bone '{bone.Name}': {(foundAnim ? "MATCHED" : "MISSING")} -> PosLen: {(foundAnim ? animT.pos.Length() : -1)}");
            }
            
            // PROBE: Specific debug for limbs
            if (!_debugLogged && (bone.Name.Contains("Leg") || bone.Name.Contains("Arm") || bone.Name.Contains("Hand")))
            {
                 MonoGameEditor.Core.Logger.Log($"[AnimatorProbe] Bone '{bone.Name}' -> Found: {foundAnim}");
                 if (foundAnim)
                 {
                     var euler = QuaternionToEuler(animT.rot);
                     MonoGameEditor.Core.Logger.Log($"      Anim Rot (Deg): {euler.X:F1}, {euler.Y:F1}, {euler.Z:F1}");
                 }
            }

            if (foundAnim)
            {
                // ALWAYS Apply Rotation and Scale from Animation
                t.rot = animT.rot;
                t.scale = animT.scale;
                
                // Smart Translation Lock:
                bool isRootOrHips = bone.ParentIndex == -1 || 
                                    bone.Name.EndsWith("Hips", System.StringComparison.OrdinalIgnoreCase) || 
                                    bone.Name.EndsWith("Root", System.StringComparison.OrdinalIgnoreCase);

                // If it's Root/Hips OR the animation provides a significant position change (not zero/near-zero), use it.
                // Otherwise, stick to Bind Pose 't.pos' to prevent collapse.
                if (isRootOrHips || animT.pos.LengthSquared() > 0.0001f)
                {
                    t.pos = animT.pos;
                }
                else
                {
                    // Keep Bind Pose t.pos (Implicit Lock)
                }
            }

            // 3. Debug Offsets (Manual Correction)
            if (debugOptions.HasValue)
            {
                var opts = debugOptions.Value;
                bool isArm = bone.Name.Contains("Arm", System.StringComparison.OrdinalIgnoreCase) || 
                             bone.Name.Contains("Hand", System.StringComparison.OrdinalIgnoreCase) || 
                             bone.Name.Contains("Shoulder", System.StringComparison.OrdinalIgnoreCase);
                             
                bool isLeg = bone.Name.Contains("Leg", System.StringComparison.OrdinalIgnoreCase) || 
                             bone.Name.Contains("Thigh", System.StringComparison.OrdinalIgnoreCase) || 
                             bone.Name.Contains("Foot", System.StringComparison.OrdinalIgnoreCase);

                if (bone.ParentIndex == -1) // Root Rotation Slider
                {
                    var debugRot = Quaternion.CreateFromYawPitchRoll(
                        Microsoft.Xna.Framework.MathHelper.ToRadians(opts.RootOffset.Y),
                        Microsoft.Xna.Framework.MathHelper.ToRadians(opts.RootOffset.X),
                        Microsoft.Xna.Framework.MathHelper.ToRadians(opts.RootOffset.Z));
                    t.rot = t.rot * debugRot;
                }
                else if (isArm)
                {
                    var debugRot = Quaternion.CreateFromYawPitchRoll(
                        Microsoft.Xna.Framework.MathHelper.ToRadians(opts.ArmOffset.Y),
                        Microsoft.Xna.Framework.MathHelper.ToRadians(opts.ArmOffset.X),
                        Microsoft.Xna.Framework.MathHelper.ToRadians(opts.ArmOffset.Z));
                    t.rot = t.rot * debugRot;
                }
                else if (isLeg)
                {
                    var debugRot = Quaternion.CreateFromYawPitchRoll(
                        Microsoft.Xna.Framework.MathHelper.ToRadians(opts.LegOffset.Y),
                        Microsoft.Xna.Framework.MathHelper.ToRadians(opts.LegOffset.X),
                        Microsoft.Xna.Framework.MathHelper.ToRadians(opts.LegOffset.Z));
                    t.rot = t.rot * debugRot;
                }
            }

            // 4. Construct Local Matrix
            Matrix4x4 local = Matrix4x4.CreateScale(t.scale) *
                              Matrix4x4.CreateFromQuaternion(t.rot) *
                              Matrix4x4.CreateTranslation(t.pos);

            // 5. Hierarchy Propagation
            if (bone.ParentIndex >= 0)
            {
                Matrix4x4 parentGlobal = ComputeBone(bone.ParentIndex, model, animated, globals, computed, boneObjects, debugOptions);
                globals[index] = local * parentGlobal;
            }
            else
            {
                globals[index] = local;
            }

            computed[index] = true;
            
            // 6. Visual Debug Update
            if (boneObjects != null && index < boneObjects.Count && boneObjects[index] != null)
            {
                var transform = boneObjects[index].Transform;
                transform.LocalPosition = new Microsoft.Xna.Framework.Vector3(t.pos.X, t.pos.Y, t.pos.Z);
                transform.LocalRotation = QuaternionToEuler(t.rot);
                transform.LocalScale = new Microsoft.Xna.Framework.Vector3(t.scale.X, t.scale.Y, t.scale.Z);
            }

            return globals[index];
        }

        private static Microsoft.Xna.Framework.Vector3 QuaternionToEuler(System.Numerics.Quaternion q)
        {
            Microsoft.Xna.Framework.Vector3 euler = new Microsoft.Xna.Framework.Vector3();

            float sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            float cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            euler.X = (float)System.Math.Atan2(sinr_cosp, cosr_cosp);

            float sinp = 2 * (q.W * q.Y - q.Z * q.X);
            if (System.Math.Abs(sinp) >= 1)
                euler.Y = (float)System.Math.CopySign(System.Math.PI / 2, sinp);
            else
                euler.Y = (float)System.Math.Asin(sinp);

            float siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            float cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            euler.Z = (float)System.Math.Atan2(siny_cosp, cosy_cosp);
            
            return new Microsoft.Xna.Framework.Vector3(
                Microsoft.Xna.Framework.MathHelper.ToDegrees(euler.X),
                Microsoft.Xna.Framework.MathHelper.ToDegrees(euler.Y),
                Microsoft.Xna.Framework.MathHelper.ToDegrees(euler.Z));
        }

        private static bool TryGetAnimatedTransform(
            Dictionary<string, (Vector3 pos, Quaternion rot, Vector3 scale)> animated, 
            string boneName, 
            out (Vector3 pos, Quaternion rot, Vector3 scale) output)
        {
            // 1. Exact Match
            if (animated.TryGetValue(boneName, out output)) return true;

            // 2. Normalize Function
            string Normalize(string s) 
            {
                // Remove namespaces
                if (s.Contains(":")) s = s.Substring(s.LastIndexOf(':') + 1);
                // Remove garbage
                return s.Replace("mixamorig", "", System.StringComparison.OrdinalIgnoreCase)
                        .Replace("_", "")
                        .Replace("-", "")
                        .Replace(" ", "")
                        .ToLowerInvariant();
            }

            string boneNorm = Normalize(boneName);

            // 3. Scan keys with normalization
            foreach (var kvp in animated)
            {
                string keyNorm = Normalize(kvp.Key);
                if (keyNorm == boneNorm)
                {
                    output = kvp.Value;
                    return true;
                }
            }
            
            // 4. Common Aliases (The "Rosetta Stone" of bones)
            var aliases = GetAliases(boneNorm);
            if (aliases != null)
            {
                foreach (var kvp in animated)
                {
                    string keyNorm = Normalize(kvp.Key);
                    foreach (var alias in aliases)
                    {
                         if (keyNorm == alias || keyNorm.Contains(alias) && alias.Length > 3) 
                         {
                             // High confidence match
                             output = kvp.Value;
                             return true;
                         }
                    }
                }
            }

            return false;
        }

        private static string[] GetAliases(string normalizedName)
        {
            // Simple mapping for common limb names
            // Normalized names are already lowercase, no underscores/namespaces
            
            if (normalizedName == "hips" || normalizedName == "root" || normalizedName == "pelvis")
                return new[] { "hips", "root", "pelvis" };

            // Left Arm
            if (normalizedName.Contains("left") || normalizedName.StartsWith("l"))
            {
                if (normalizedName.Contains("arm") || normalizedName.Contains("shoulder"))
                {
                     if (normalizedName.Contains("up") || normalizedName == "leftarm" || normalizedName == "larm")
                         return new[] { "leftarm", "leftupperarm", "upperarml", "lshoulder", "larm" };
                     if (normalizedName.Contains("fore") || normalizedName.Contains("low") || normalizedName == "leftelbow")
                         return new[] { "leftforearm", "leftlowerarm", "lowerarml", "forearml", "lelbow" };
                     if (normalizedName.Contains("hand") || normalizedName.Contains("wrist"))
                         return new[] { "lefthand", "lhand", "lwrist", "leftwrist" };
                }
                if (normalizedName.Contains("leg") || normalizedName.Contains("thigh"))
                {
                     if (normalizedName.Contains("up") || normalizedName == "leftleg" || normalizedName == "lleg" || normalizedName.Contains("thigh"))
                         return new[] { "leftleg", "leftup", "leftthigh", "thighl", "uplegl", "lleg" };
                     if (normalizedName.Contains("low") || normalizedName.Contains("shin") || normalizedName.Contains("calf"))
                         return new[] { "leftleglow", "leftshin", "shinl", "calfl", "lowlegl", "lknee" };
                     if (normalizedName.Contains("foot") || normalizedName.Contains("ankle"))
                         return new[] { "leftfoot", "lfoot", "footl", "lankle", "ankle" };
                }
            }
            
            // Right Arm
            if (normalizedName.Contains("right") || normalizedName.StartsWith("r"))
            {
                if (normalizedName.Contains("arm") || normalizedName.Contains("shoulder"))
                {
                     if (normalizedName.Contains("up") || normalizedName == "rightarm" || normalizedName == "rarm")
                         return new[] { "rightarm", "rightupperarm", "upperarmr", "rshoulder", "rarm" };
                     if (normalizedName.Contains("fore") || normalizedName.Contains("low") || normalizedName == "rightelbow")
                         return new[] { "rightforearm", "rightlowerarm", "lowerarmr", "forearmr", "relbow" };
                     if (normalizedName.Contains("hand") || normalizedName.Contains("wrist"))
                         return new[] { "righthand", "rhand", "rwrist", "rightwrist" };
                }
                if (normalizedName.Contains("leg") || normalizedName.Contains("thigh"))
                {
                     if (normalizedName.Contains("up") || normalizedName == "rightleg" || normalizedName == "rleg" || normalizedName.Contains("thigh"))
                         return new[] { "rightleg", "rightup", "rightthigh", "thighr", "uplegr", "rleg" };
                     if (normalizedName.Contains("low") || normalizedName.Contains("shin") || normalizedName.Contains("calf"))
                         return new[] { "rightleglow", "rightshin", "shinr", "calfr", "lowlegr", "rknee" };
                     if (normalizedName.Contains("foot") || normalizedName.Contains("ankle"))
                         return new[] { "rightfoot", "rfoot", "footr", "rankle", "ankle" };
                }
            }

            return null;
        }
    }
}
