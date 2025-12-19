using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using MonoGameEditor.Core;
using MonoGameEditor.Core.Components;

namespace MyGame.Scripts
{
    /// <summary>
    /// Controle de personagem com física - WASD para movimento, Mouse para rotação
    /// </summary>
    public class PlayerController : ScriptComponent
    {
        // Configurações de Movimento
        public float MoveSpeed = 8.0f;          // Velocidade de movimento
        public float RotationSpeed = 2.0f;      // Velocidade de rotação (mouse/teclado)
        public float JumpForce = 5.0f;          // Força do pulo
        
        // Configurações de Física
        public float AirControl = 0.3f;         // Controle no ar (0 = nenhum, 1 = total)
        public float GroundDrag = 0.9f;         // Arrasto no chão
        public float AirDrag = 0.98f;           // Arrasto no ar
        
        // Componentes
        private PhysicsBodyComponent _physics;
        private bool _isGrounded = false;
        private float _rotationY = 0f;
        
        public override void Start()
        {
            // Pegar referência ao PhysicsBody
            _physics = GameObject?.GetComponent<PhysicsBodyComponent>();
            
            if (_physics == null)
            {
                Logger.Log("[PlayerController] ERRO: PhysicsBodyComponent não encontrado!");
                return;
            }
            
            // Configurar física para personagem
            _physics.UseGravity = true;
            _physics.Drag = 0.1f; // Baixo drag pois vamos controlar manualmente
            _physics.Mass = 70f; // Massa de uma pessoa (~70kg)
            
            // Travar rotação para evitar o personagem tombar
            _physics.FreezeRotationX = true;
            _physics.FreezeRotationY = false; // Permitir rotação Y (girar)
            _physics.FreezeRotationZ = true;
            
            Logger.Log("[PlayerController] Iniciado com sucesso!");
        }

        public override void Update(GameTime gameTime)
        {
            if (_physics == null) return;
            
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // === INPUT DE MOVIMENTO ===
            Vector3 inputDirection = Vector3.Zero;
            
            if (Input.GetKey(Keys.W)) inputDirection.Z -= 1;
            if (Input.GetKey(Keys.S)) inputDirection.Z += 1;
            if (Input.GetKey(Keys.A)) inputDirection.X -= 1;
            if (Input.GetKey(Keys.D)) inputDirection.X += 1;
            
            // === ROTAÇÃO (Setas ou Q/E) ===
            if (Input.GetKey(Keys.Left) || Input.GetKey(Keys.Q))
                _rotationY -= RotationSpeed * dt;
            
            if (Input.GetKey(Keys.Right) || Input.GetKey(Keys.E))
                _rotationY += RotationSpeed * dt;
            
            // Aplicar rotação ao GameObject
            transform.LocalRotation = new Vector3(
                transform.LocalRotation.X,
                MathHelper.ToDegrees(_rotationY),
                transform.LocalRotation.Z
            );
            
            // === MOVIMENTO COM FÍSICA ===
            if (inputDirection != Vector3.Zero)
            {
                // Normalizar input diagonal
                inputDirection.Normalize();
                
                // Converter para direção do mundo baseado na rotação do personagem
                Matrix rotationMatrix = Matrix.CreateRotationY(_rotationY);
                Vector3 worldDirection = Vector3.Transform(inputDirection, rotationMatrix);
                
                // Calcular velocidade desejada (só X e Z, manter Y da física)
                Vector3 targetVelocity = new Vector3(
                    worldDirection.X * MoveSpeed,
                    _physics.Velocity.Y, // Manter velocidade Y (gravidade/pulo)
                    worldDirection.Z * MoveSpeed
                );
                
                // Interpolar para movimento suave (mais controle no chão, menos no ar)
                float controlFactor = _isGrounded ? 1.0f : AirControl;
                _physics.Velocity = new Vector3(
                    MathHelper.Lerp(_physics.Velocity.X, targetVelocity.X, controlFactor),
                    _physics.Velocity.Y,
                    MathHelper.Lerp(_physics.Velocity.Z, targetVelocity.Z, controlFactor)
                );
            }
            else
            {
                // Aplicar drag quando não há input (parar gradualmente)
                float dragFactor = _isGrounded ? GroundDrag : AirDrag;
                _physics.Velocity = new Vector3(
                    _physics.Velocity.X * dragFactor,
                    _physics.Velocity.Y,
                    _physics.Velocity.Z * dragFactor
                );
            }
            
            // === PULO ===
            if (Input.GetKeyDown(Keys.Space) && _isGrounded)
            {
                // Aplicar força de pulo
                _physics.Velocity = new Vector3(
                    _physics.Velocity.X,
                    JumpForce,
                    _physics.Velocity.Z
                );
                
                _isGrounded = false;
                Logger.Log("[PlayerController] Pulo!");
            }
            
            // === DETECÇÃO DE CHÃO (Simples) ===
            // Verificar se velocidade Y é próxima de zero e posição Y é estável
            _isGrounded = System.Math.Abs(_physics.Velocity.Y) < 0.1f && transform.Position.Y < 2.0f;
        }
        
        /// <summary>
        /// Chamado quando colide com algo (se implementado no engine)
        /// </summary>
        public void OnCollisionEnter(GameObject other)
        {
            // Detectar chão por colisão
            if (other.Name.Contains("Plane") || other.Name.Contains("Ground"))
            {
                _isGrounded = true;
            }
        }
    }
}
