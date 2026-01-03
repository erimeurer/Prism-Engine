# Sistema de Importação de Animações - Prism Engine

## Visão Geral

O sistema de importação de animações permite importar e reproduzir animações de arquivos FBX com suporte completo a:
- ✅ Múltiplas animações por modelo
- ✅ Interpolação suave entre keyframes
- ✅ Fade/blending entre animações
- ✅ Controle de velocidade de reprodução
- ✅ Animações em loop

## Arquivos Criados

### 1. Core/Assets/AnimationData.cs
Contém as estruturas de dados para animações:
- **AnimationKeyframe**: Representa um keyframe individual (posição, rotação, escala)
- **AnimationChannel**: Representa um canal de animação para um osso específico
- **AnimationClip**: Representa um clipe de animação completo
- **AnimationCollection**: Container para todas as animações de um modelo

### 2. Core/Components/AnimatorComponent.cs
Componente que controla a reprodução de animações:
- Play/Pause/Stop
- Fade entre animações
- Controle de velocidade
- Aplicação de transformações aos ossos

### 3. Core/Assets/ModelImporter.cs (Modificado)
Adicionado suporte para importar animações de arquivos FBX:
- Extração de canais de animação
- Interpolação de keyframes
- Conversão de dados do Assimp para formato interno

### 4. Core/Assets/ModelData.cs (Modificado)
Adicionada propriedade `Animations` para armazenar animações importadas

## Como Usar

### 1. Importar um Modelo com Animações

Ao importar um arquivo FBX com animações usando o `ModelImporter`, as animações serão automaticamente extraídas:

```csharp
var modelData = await ModelImporter.LoadModelDataAsync("path/to/animated_model.fbx");

// Verificar se há animações
if (modelData.Animations != null)
{
    Logger.Log($"Modelo tem {modelData.Animations.Animations.Count} animações");
    
    foreach (var anim in modelData.Animations.Animations)
    {
        Logger.Log($"  - {anim.Name}: {anim.Duration}s");
    }
}
```

### 2. Adicionar Componente Animator

```csharp
// Criar GameObject com SkinnedModelRenderer
var character = new GameObject("Character");
var skinnedRenderer = new SkinnedModelRendererComponent();

// Configurar renderer com modelo e ossos...
character.AddComponent(skinnedRenderer);

// Adicionar Animator
var animator = new AnimatorComponent();
animator.AnimationCollection = modelData.Animations;
character.AddComponent(animator);
```

### 3. Controlar Animações

#### Tocar uma Animação

```csharp
// Por nome (com fade)
animator.Play("Walk", fade: true);

// Por índice (sem fade)
animator.Play(0, fade: false);

// Ajustar velocidade
animator.AnimationSpeed = 1.5f; // 1.5x mais rápido

// Ajustar duração do fade
animator.FadeDuration = 0.5f; // 500ms
```

#### Pausar/Retomar

```csharp
animator.Pause();
animator.Resume();
```

#### Parar

```csharp
animator.Stop();
```

### 4. Atualizar Animator (Game Loop)

**IMPORTANTE**: O Animator precisa ser atualizado a cada frame:

```csharp
// No seu game loop ou Update():
float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
animator.Update(deltaTime);
```

## Exemplo Completo

```csharp
using MonoGameEditor.Core;
using MonoGameEditor.Core.Components;
using MonoGameEditor.Core.Assets;

public class CharacterController
{
    private GameObject character;
    private AnimatorComponent animator;
    
    public async Task Initialize()
    {
        // 1. Importar modelo com animações
        var modelData = await ModelImporter.LoadModelDataAsync("Content/Models/character.fbx");
        
        // 2. Criar GameObject
        character = new GameObject("Player");
        
        // 3. Configurar SkinnedModelRenderer
        var skinnedRenderer = new SkinnedModelRendererComponent();
        skinnedRenderer.LoadModel("Content/Models/character.fbx");
        // ... configurar ossos, materiais, etc ...
        character.AddComponent(skinnedRenderer);
        
        // 4. Adicionar Animator
        animator = new AnimatorComponent();
        animator.AnimationCollection = modelData.Animations;
        animator.FadeDuration = 0.3f; // 300ms de fade
        character.AddComponent(animator);
        
        // 5. Tocar animação inicial
        animator.Play("Idle", fade: false);
    }
    
    public void Update(GameTime gameTime)
    {
        // Atualizar animator
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        animator.Update(deltaTime);
        
        // Exemplo: trocar animações baseado em input
        if (IsMoving())
        {
            if (animator.CurrentAnimationIndex != GetAnimationIndex("Walk"))
            {
                animator.Play("Walk", fade: true); // Com fade suave
            }
        }
        else
        {
            if (animator.CurrentAnimationIndex != GetAnimationIndex("Idle"))
            {
                animator.Play("Idle", fade: true);
            }
        }
    }
    
    private int GetAnimationIndex(string name)
    {
        if (animator.AnimationCollection != null &&
            animator.AnimationCollection.AnimationNameToIndex.TryGetValue(name, out int index))
        {
            return index;
        }
        return -1;
    }
    
    private bool IsMoving()
    {
        // Sua lógica de movimento aqui
        return false;
    }
}
```

## Propriedades do Animator

| Propriedade | Tipo | Descrição |
|------------|------|-----------|
| `AnimationCollection` | `AnimationCollection?` | Coleção de animações disponíveis |
| `AnimationNames` | `List<string>` | Lista de nomes das animações (somente leitura) |
| `CurrentAnimationIndex` | `int` | Índice da animação atual |
| `AnimationSpeed` | `float` | Velocidade de reprodução (1.0 = normal) |
| `FadeDuration` | `float` | Duração do fade entre animações (segundos) |
| `IsPlaying` | `bool` | Se está tocando animação (somente leitura) |
| `IsPaused` | `bool` | Se está pausado (somente leitura) |
| `CurrentTime` | `float` | Tempo atual da animação (segundos) |

## Métodos do Animator

| Método | Descrição |
|--------|-----------|
| `Play(string name, bool fade = true)` | Toca animação por nome |
| `Play(int index, bool fade = true)` | Toca animação por índice |
| `Pause()` | Pausa a animação atual |
| `Resume()` | Retoma a animação pausada |
| `Stop()` | Para a animação e reseta o tempo |
| `Update(float deltaTime)` | Atualiza o animator (chamar todo frame) |

## Fade/Blending

O sistema de fade permite transições suaves entre animações:

```csharp
// Transição instantânea (sem fade)
animator.Play("Jump", fade: false);

// Transição suave (com fade)
animator.Play("Land", fade: true);

// Ajustar duração do fade
animator.FadeDuration = 0.2f; // Fade mais rápido (200ms)
animator.Play("Run", fade: true);
```

Durante o fade, as transformações das duas animações são interpoladas linearmente (posição e escala) e esfericamente (rotação).

## Notas Importantes

1. **O Animator precisa de SkinnedModelRendererComponent**: O componente Animator busca o SkinnedModelRendererComponent no mesmo GameObject para aplicar as transformações aos ossos.

2. **Atualização a cada frame**: Sempre chame `animator.Update(deltaTime)` no game loop.

3. **Animações em Loop**: Por padrão, todas as animações importadas são configuradas como loop. Para mudar: 
   ```csharp
   var anim = animator.AnimationCollection.GetAnimation("Death");
   if (anim != null)
       anim.IsLooping = false;
   ```

4. **Nomes de Animações**: Os nomes das animações são extraídos do arquivo FBX. Se o arquivo não tiver nomes, serão usados "Animation_0", "Animation_1", etc.

## Troubleshooting

**Problema**: Animação não está tocando
- ✓ Verifique se `animator.Update(deltaTime)` está sendo chamado
- ✓ Verifique se `AnimationCollection` foi definida
- ✓ Verifique se o nome da animação está correto

**Problema**: Modelo está deformado
- ✓ Verifique se os ossos estão configurados corretamente no SkinnedModelRenderer
- ✓ Verifique se o modelo foi importado com a flag `FBXPreservePivotsConfig(false)`

**Problema**: Fade não está funcionando
- ✓ Verifique se `FadeDuration` > 0
- ✓ Verifique se você está passando `fade: true` ao chamar `Play()`

## Referências

- **AnimationData.cs**: Estruturas de dados de animação
- **AnimatorComponent.cs**: Componente de controle de animações
- **ModelImporter.cs**: Importação de animações FBX
- **SkinnedModelRendererComponent.cs**: Renderização de modelos com ossos
