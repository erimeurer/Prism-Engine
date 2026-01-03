using System;
using System.Collections.Generic;

namespace MonoGameEditor.Core.Assets
{
    /// <summary>
    /// Representa um keyframe de animação para um osso específico
    /// </summary>
    public class AnimationKeyframe
    {
        public float Time { get; set; }
        public System.Numerics.Vector3 Position { get; set; }
        public System.Numerics.Quaternion Rotation { get; set; }
        public System.Numerics.Vector3 Scale { get; set; }
    }

    /// <summary>
    /// Representa um canal de animação para um osso específico
    /// </summary>
    public class AnimationChannel
    {
        public string BoneName { get; set; } = string.Empty;
        public List<AnimationKeyframe> Keyframes { get; set; } = new List<AnimationKeyframe>();

        /// <summary>
        /// Interpola entre keyframes para obter transformação em um tempo específico
        /// </summary>
        public void GetTransformAtTime(float time, out System.Numerics.Vector3 position, 
            out System.Numerics.Quaternion rotation, out System.Numerics.Vector3 scale)
        {
            if (Keyframes.Count == 0)
            {
                position = System.Numerics.Vector3.Zero;
                rotation = System.Numerics.Quaternion.Identity;
                scale = System.Numerics.Vector3.One;
                return;
            }

            if (Keyframes.Count == 1 || time <= Keyframes[0].Time)
            {
                position = Keyframes[0].Position;
                rotation = Keyframes[0].Rotation;
                scale = Keyframes[0].Scale;
                return;
            }

            if (time >= Keyframes[^1].Time)
            {
                position = Keyframes[^1].Position;
                rotation = Keyframes[^1].Rotation;
                scale = Keyframes[^1].Scale;
                return;
            }

            // Encontrar keyframes adjacentes
            for (int i = 0; i < Keyframes.Count - 1; i++)
            {
                if (time >= Keyframes[i].Time && time <= Keyframes[i + 1].Time)
                {
                    var k1 = Keyframes[i];
                    var k2 = Keyframes[i + 1];
                    
                    float duration = k2.Time - k1.Time;
                    float t = (time - k1.Time) / duration;

                    // Interpolação linear para posição e escala
                    position = System.Numerics.Vector3.Lerp(k1.Position, k2.Position, t);
                    scale = System.Numerics.Vector3.Lerp(k1.Scale, k2.Scale, t);
                    
                    // Interpolação esférica (Slerp) para rotação
                    rotation = System.Numerics.Quaternion.Slerp(k1.Rotation, k2.Rotation, t);
                    return;
                }
            }

            // Fallback
            position = Keyframes[0].Position;
            rotation = Keyframes[0].Rotation;
            scale = Keyframes[0].Scale;
        }
    }

    /// <summary>
    /// Representa um clipe de animação completo
    /// </summary>
    public class AnimationClip
    {
        public string Name { get; set; } = string.Empty;
        public float Duration { get; set; }
        public float TicksPerSecond { get; set; } = 25.0f;
        public List<AnimationChannel> Channels { get; set; } = new List<AnimationChannel>();
        public bool IsLooping { get; set; } = true;

        /// <summary>
        /// Obtém transformações de todos os canais em um tempo específico.
        /// Se houver múltiplos canais para o mesmo osso (ex: Assimp split nodes), eles são combinados.
        /// </summary>
        public Dictionary<string, (System.Numerics.Vector3 position, System.Numerics.Quaternion rotation, System.Numerics.Vector3 scale)> 
            GetTransformsAtTime(float time)
        {
            var transforms = new Dictionary<string, (System.Numerics.Vector3 position, System.Numerics.Quaternion rotation, System.Numerics.Vector3 scale)>();
            
            foreach (var channel in Channels)
            {
                channel.GetTransformAtTime(time, out var pos, out var rot, out var scale);
                transforms[channel.BoneName] = (pos, rot, scale);
            }
            
            return transforms;
        }
    }

    /// <summary>
    /// Container para todas as animações de um modelo
    /// </summary>
    public class AnimationCollection
    {
        public List<AnimationClip> Animations { get; set; } = new List<AnimationClip>();
        public Dictionary<string, int> AnimationNameToIndex { get; set; } = new Dictionary<string, int>();

        public AnimationClip? GetAnimation(string name)
        {
            if (AnimationNameToIndex.TryGetValue(name, out int index) && index < Animations.Count)
            {
                return Animations[index];
            }
            return null;
        }

        public AnimationClip? GetAnimation(int index)
        {
            if (index >= 0 && index < Animations.Count)
            {
                return Animations[index];
            }
            return null;
        }
    }
}
