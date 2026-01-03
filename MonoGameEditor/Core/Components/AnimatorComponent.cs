using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MonoGameEditor.Core.Assets;

namespace MonoGameEditor.Core.Components
{
    /// <summary>
    /// Componente Animator - Controla animações com suporte a fade/blending
    /// </summary>
    public class AnimatorComponent : Component
    {
        private AnimationCollection? _animationCollection;
        private AnimationClip? _currentAnimation;
        private AnimationClip? _previousAnimation;
        private int _currentAnimationIndex = -1;
        private float _currentTime = 0f;
        private float _animationSpeed = 1.0f;
        private bool _isPlaying = false;
        private bool _isPaused = false;
        
        // Fade/Blending properties
        private bool _isFading = false;
        private float _fadeTime = 0f;
        private float _fadeDuration = 0.3f; // 300ms de fade por padrão
        private float _previousAnimationTime = 0f;
        
        // Debug
        private int _debugLogCount = 0;
        private Vector3 _armRotationOffset = Vector3.Zero;
        private Vector3 _legRotationOffset = Vector3.Zero;
        private Vector3 _rootRotationOffset = Vector3.Zero;

        public override string ComponentName => "Animator";

        /// <summary>
        /// Coleção de animações disponíveis
        /// </summary>
        public AnimationCollection? AnimationCollection
        {
            get => _animationCollection;
            set
            {
                _animationCollection = value;
                OnPropertyChanged(nameof(AnimationCollection));
                OnPropertyChanged(nameof(AnimationNames));
            }
        }

        /// <summary>
        /// Lista de nomes de animações (para UI)
        /// </summary>
        public List<string> AnimationNames
        {
            get
            {
                if (_animationCollection == null)
                    return new List<string>();
                
                return _animationCollection.Animations.Select(a => a.Name).ToList();
            }
        }

        /// <summary>
        /// Índice da animação atual
        /// </summary>
        public int CurrentAnimationIndex
        {
            get => _currentAnimationIndex;
            set
            {
                if (_currentAnimationIndex != value)
                {
                    _currentAnimationIndex = value;
                    OnPropertyChanged(nameof(CurrentAnimationIndex));
                }
            }
        }

        /// <summary>
        /// Velocidade de reprodução (1.0 = normal, 2.0 = 2x, 0.5 = lento)
        /// </summary>
        public float AnimationSpeed
        {
            get => _animationSpeed;
            set
            {
                _animationSpeed = MathHelper.Max(0.001f, value);
                OnPropertyChanged(nameof(AnimationSpeed));
            }
        }

        /// <summary>
        /// Duração do fade entre animações (em segundos)
        /// </summary>
        public float FadeDuration
        {
            get => _fadeDuration;
            set
            {
                _fadeDuration = MathHelper.Max(0.01f, value);
                OnPropertyChanged(nameof(FadeDuration));
            }
        }

        /// <summary>
        /// Se está tocando animação
        /// </summary>
        public bool IsPlaying
        {
            get => _isPlaying;
            private set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    OnPropertyChanged(nameof(IsPlaying));
                }
            }
        }

        /// <summary>
        /// Se está pausado
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused;
            private set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    OnPropertyChanged(nameof(IsPaused));
                }
            }
        }

        /// <summary>
        /// Tempo atual da animação (em segundos)
        /// </summary>
        public float CurrentTime
        {
            get => _currentTime;
            set
            {
                _currentTime = value;
                OnPropertyChanged(nameof(CurrentTime));
            }
        }

        /// <summary>
        /// Debug: Rotação extra para braços (Euler em graus)
        /// </summary>
        public Vector3 ArmRotationOffset
        {
            get => _armRotationOffset;
            set
            {
                _armRotationOffset = value;
                OnPropertyChanged(nameof(ArmRotationOffset));
                OnPropertyChanged(nameof(ArmRotationX));
                OnPropertyChanged(nameof(ArmRotationY));
                OnPropertyChanged(nameof(ArmRotationZ));
            }
        }

        public float ArmRotationX
        {
            get => _armRotationOffset.X;
            set { var v = _armRotationOffset; v.X = value; ArmRotationOffset = v; }
        }

        public float ArmRotationY
        {
            get => _armRotationOffset.Y;
            set { var v = _armRotationOffset; v.Y = value; ArmRotationOffset = v; }
        }

        public float ArmRotationZ
        {
            get => _armRotationOffset.Z;
            set { var v = _armRotationOffset; v.Z = value; ArmRotationOffset = v; }
        }

        /// <summary>
        /// Debug: Rotação extra para pernas (Euler em graus)
        /// </summary>
        public Vector3 LegRotationOffset
        {
            get => _legRotationOffset;
            set
            {
                _legRotationOffset = value;
                OnPropertyChanged(nameof(LegRotationOffset));
                OnPropertyChanged(nameof(LegRotationX));
                OnPropertyChanged(nameof(LegRotationY));
                OnPropertyChanged(nameof(LegRotationZ));
            }
        }

        public float LegRotationX
        {
            get => _legRotationOffset.X;
            set { var v = _legRotationOffset; v.X = value; LegRotationOffset = v; }
        }

        public float LegRotationY
        {
            get => _legRotationOffset.Y;
            set { var v = _legRotationOffset; v.Y = value; LegRotationOffset = v; }
        }

        public float LegRotationZ
        {
            get => _legRotationOffset.Z;
            set { var v = _legRotationOffset; v.Z = value; LegRotationOffset = v; }
        }

        /// <summary>
        /// Debug: Rotação extra para o Root/Hips (Euler em graus)
        /// </summary>
        public Vector3 RootRotationOffset
        {
            get => _rootRotationOffset;
            set
            {
                _rootRotationOffset = value;
                OnPropertyChanged(nameof(RootRotationOffset));
                OnPropertyChanged(nameof(RootRotationX));
                OnPropertyChanged(nameof(RootRotationY));
                OnPropertyChanged(nameof(RootRotationZ));
            }
        }

        public float RootRotationX
        {
            get => _rootRotationOffset.X;
            set { var v = _rootRotationOffset; v.X = value; RootRotationOffset = v; }
        }

        public float RootRotationY
        {
            get => _rootRotationOffset.Y;
            set { var v = _rootRotationOffset; v.Y = value; RootRotationOffset = v; }
        }

        public float RootRotationZ
        {
            get => _rootRotationOffset.Z;
            set { var v = _rootRotationOffset; v.Z = value; RootRotationOffset = v; }
        }

        /// <summary>
        /// Toca uma animação pelo nome com fade opcional
        /// </summary>
        public void Play(string animationName, bool fade = true)
        {
            if (_animationCollection == null)
            {
                Logger.LogWarning($"[Animator] AnimationCollection não definida!");
                return;
            }

            var animation = _animationCollection.GetAnimation(animationName);
            if (animation == null)
            {
                Logger.LogWarning($"[Animator] Animação '{animationName}' não encontrada!");
                return;
            }

            // Se já está tocando esta animação, não fazer nada
            if (_currentAnimation == animation && _isPlaying)
                return;

            // Configurar fade se solicitado
            if (fade && _currentAnimation != null && _isPlaying)
            {
                _previousAnimation = _currentAnimation;
                _previousAnimationTime = _currentTime;
                _isFading = true;
                _fadeTime = 0f;
            }

            _currentAnimation = animation;
            _currentAnimationIndex = _animationCollection.AnimationNameToIndex[animationName];
            _currentTime = 0f;
            _isPlaying = true;
            _isPaused = false;

            Logger.Log($"[Animator] Tocando animação '{animationName}' {(fade && _isFading ? "com fade" : "sem fade")}");
        }

        /// <summary>
        /// Toca uma animação pelo índice com fade opcional
        /// </summary>
        public void Play(int animationIndex, bool fade = true)
        {
            if (_animationCollection == null)
            {
                Logger.LogWarning($"[Animator] AnimationCollection não definida!");
                return;
            }

            var animation = _animationCollection.GetAnimation(animationIndex);
            if (animation == null)
            {
                Logger.LogWarning($"[Animator] Animação com índice {animationIndex} não encontrada!");
                return;
            }

            Play(animation.Name, fade);
        }

        /// <summary>
        /// Pausa a animação atual
        /// </summary>
        public void Pause()
        {
            if (_isPlaying)
            {
                _isPaused = true;
                Logger.Log($"[Animator] Animação pausada");
            }
        }

        /// <summary>
        /// Resume a animação pausada
        /// </summary>
        public void Resume()
        {
            if (_isPaused)
            {
                _isPaused = false;
                Logger.Log($"[Animator] Animação retomada");
            }
        }

        /// <summary>
        /// Para a animação atual
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;
            _isPaused = false;
            _currentTime = 0f;
            _isFading = false;
            Logger.Log($"[Animator] Animação parada");
        }

        /// <summary>
        /// Atualiza o Animator (deve ser chamado a cada frame)
        /// </summary>
        public void Update(float deltaTime)
        {
            // DEBUG: Why is it not updating?
            if (_debugLogCount < 50)
            {
                 // Trace heartbeat
                 if (_debugLogCount % 10 == 0) 
                     MonoGameEditor.Core.Logger.Log($"[AnimatorComponent] Update Tick. Playing={_isPlaying}, Paused={_isPaused}, Anim={_currentAnimation?.Name}, Time={_currentTime:F2}");
                 
                // Only log if something is WRONG (one of these is preventing update)
                if (!_isPlaying || _isPaused || _currentAnimation == null)
                {
                     MonoGameEditor.Core.Logger.Log($"[AnimatorComponent] Update SKIPPED! IsPlaying={_isPlaying}, Paused={_isPaused}, Anim={_currentAnimation?.Name ?? "NULL"}");
                     _debugLogCount++;
                }
            }

            if (!_isPlaying || _isPaused || _currentAnimation == null)
                return;

            // Atualizar tempo da animação
            _currentTime += deltaTime * _animationSpeed;

            // Verificar loop
            if (_currentTime >= _currentAnimation.Duration)
            {
                if (_currentAnimation.IsLooping)
                {
                    _currentTime = _currentTime % _currentAnimation.Duration;
                }
                else
                {
                    _currentTime = _currentAnimation.Duration;
                    _isPlaying = false;
                }
            }

            // Atualizar fade
            if (_isFading)
            {
                _fadeTime += deltaTime;
                
                if (_fadeTime >= _fadeDuration)
                {
                    _isFading = false;
                    _previousAnimation = null;
                }
            }

            // Aplicar animação aos ossos
            ApplyAnimationToBones();
        }

        /// <summary>
        /// Aplica as transformações da animação aos ossos do modelo
        /// </summary>
        private void ApplyAnimationToBones()
        {
            if (_currentAnimation == null)
                return;

            var skinnedRenderer = GameObject?.GetComponent<SkinnedModelRendererComponent>();
            if (skinnedRenderer == null)
            {
                if (_debugLogCount < 10) MonoGameEditor.Core.Logger.LogWarning($"[AnimatorComponent] No SkinnedModelRenderer found on {GameObject?.Name}!");
                return;
            }

            if (skinnedRenderer.Model == null)
            {
                 if (_debugLogCount < 10) MonoGameEditor.Core.Logger.LogWarning($"[AnimatorComponent] SkinnedModelRenderer has no Model data!");
                 return;
            }

            // Garantir buffer de matrizes
            if (skinnedRenderer.FinalBoneMatrices == null ||
                skinnedRenderer.FinalBoneMatrices.Length != skinnedRenderer.Model.Bones.Count)
            {
                skinnedRenderer.FinalBoneMatrices =
                    new System.Numerics.Matrix4x4[skinnedRenderer.Model.Bones.Count];
            }
            
            // DEBUG: Trace execution
            if (_debugLogCount < 50)
            {
                _debugLogCount++;
                MonoGameEditor.Core.Logger.Log($"[AnimatorComponent] ApplyAnimationToBones called. Time: {_currentTime:F2}, Bones: {skinnedRenderer.Bones?.Count}");
            }

    // Tempo atual
    float time = _currentTime;

    // Fade/blend
    if (_isFading && _previousAnimation != null)
    {
        float blend = MathHelper.Clamp(_fadeTime / _fadeDuration, 0f, 1f);

        var debugOptions = new PoseDebugOptions
        {
            ArmOffset = new System.Numerics.Vector3(_armRotationOffset.X, _armRotationOffset.Y, _armRotationOffset.Z),
            LegOffset = new System.Numerics.Vector3(_legRotationOffset.X, _legRotationOffset.Y, _legRotationOffset.Z),
            RootOffset = new System.Numerics.Vector3(_rootRotationOffset.X, _rootRotationOffset.Y, _rootRotationOffset.Z)
        };

        AnimationPoseBuilder.BuildBlendedPose(
            skinnedRenderer.Model,
            _previousAnimation,
            _currentAnimation,
            _previousAnimationTime,
            time,
            blend,
            skinnedRenderer.FinalBoneMatrices,
            debugOptions
        );
    }
    else
    {
        var debugOptions = new PoseDebugOptions
        {
            ArmOffset = new System.Numerics.Vector3(_armRotationOffset.X, _armRotationOffset.Y, _armRotationOffset.Z),
            LegOffset = new System.Numerics.Vector3(_legRotationOffset.X, _legRotationOffset.Y, _legRotationOffset.Z),
            RootOffset = new System.Numerics.Vector3(_rootRotationOffset.X, _rootRotationOffset.Y, _rootRotationOffset.Z)
        };

        AnimationPoseBuilder.BuildFinalPose(
            skinnedRenderer.Model,
            _currentAnimation,
            time,
            skinnedRenderer.FinalBoneMatrices,
            skinnedRenderer.Bones,
            debugOptions
        );
    }
}


        /// <summary>
        /// Converte Quaternion para ângulos de Euler (em radianos)
        /// </summary>
        private System.Numerics.Vector3 QuaternionToEuler(System.Numerics.Quaternion q)
        {
            System.Numerics.Vector3 euler;

            // Roll (X)
            float sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            float cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            euler.X = (float)Math.Atan2(sinr_cosp, cosr_cosp);

            // Pitch (Y)
            float sinp = 2 * (q.W * q.Y - q.Z * q.X);
            if (Math.Abs(sinp) >= 1)
                euler.Y = (float)Math.CopySign(Math.PI / 2, sinp);
            else
                euler.Y = (float)Math.Asin(sinp);

            // Yaw (Z)
            float siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            float cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            euler.Z = (float)Math.Atan2(siny_cosp, cosy_cosp);

            return euler;
        }
    }
}
