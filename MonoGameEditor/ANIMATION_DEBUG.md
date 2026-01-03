# 🔍 Debug: Como Verificar se Animações Estão Sendo Importadas

## Problema
Você importou um modelo com animação mas "não deu nada" - as animações não aparecem.

## Solução: Verificar Logs de Debug

### Passo 1: Abrir a Janela de Output no Visual Studio

1. No Visual Studio, vá em **View → Output** (ou pressione `Ctrl+Alt+O`)
2. Na janela Output, selecione **Debug** no dropdown "Show output from:"

### Passo 2: Importar seu Modelo

1. Execute o editor/engine
2. Importe seu arquivo FBX com animações
3. Observe a janela de Output

### Passo 3: Analisar os Logs

Você verá logs como estes:

```
[ModelImporter] Cache MISS: character.fbx - loading...
[ModelImporter]   Full path: D:\Models\character.fbx
[ModelImporter]   File exists: True
[ModelImporter] Verificando animações em 'character.fbx'...
[ModelImporter]   scene.HasAnimations = True
[ModelImporter]   scene.AnimationCount = 3
[ModelImporter] ✓ Encontradas 3 animações! Extraindo...
[ModelImporter]   Animação 'Idle' - Duração: 2.50s, Canais: 45
[ModelImporter]     ✓ Extraídos 45 canais de animação
[ModelImporter]   Animação 'Walk' - Duração: 1.20s, Canais: 45
[ModelImporter]     ✓ Extraídos 45 canais de animação
[ModelImporter]   Animação 'Run' - Duração: 0.80s, Canais: 45
[ModelImporter]     ✓ Extraídos 45 canais de animação
[ModelImporter] ✓ Total de 3 animações extraídas
[ModelImporter] ✓ AnimationCollection criada com 3 animações
```

## Cenários Possíveis

### ✅ Cenário 1: Animações Encontradas
```
[ModelImporter]   scene.HasAnimations = True
[ModelImporter]   scene.AnimationCount = 3
[ModelImporter] ✓ AnimationCollection criada com 3 animações
```
**Ação:** As animações foram importadas! Agora você precisa:
1. Adicionar o componente `AnimatorComponent` ao GameObject
2. Atribuir `modelData.Animations` ao `animator.AnimationCollection`
3. Chamar `animator.Update(deltaTime)` no game loop

### ⚠️ Cenário 2: Nenhuma Animação no Arquivo
```
[ModelImporter]   scene.HasAnimations = False
[ModelImporter]   scene.AnimationCount = 0
[ModelImporter] ⚠ Nenhuma animação encontrada no arquivo
```
**Possíveis Causas:**
- O arquivo FBX não contém animações
- As animações não foram exportadas corretamente do software 3D (Blender, Maya, etc.)
- O arquivo só contém o modelo/mesh sem animações

**Solução:**
- Re-exporte o modelo do Blender/Maya garantindo que "Export Animation" está marcado
- Verifique no software 3D se as animações existem na timeline
- Teste com outro arquivo FBX que você saiba que tem animações (exemplo: modelos do Mixamo)

### ❌ Cenário 3: Erro Durante Extração
```
[ModelImporter]   scene.HasAnimations = True
[ModelImporter]   scene.AnimationCount = 2
[ModelImporter] ⚠ ERRO: AnimationCollection está NULL após ExtractAnimations!
```
**Causa:** Bug no código de extração
**Solução:** Verificar exceções na janela de Output e reportar o erro

### 🔴 Cenário 4: Arquivo Não Encontrado
```
[ModelImporter]   Full path: D:\Models\character.fbx
[ModelImporter]   File exists: False
```
**Causa:** O caminho do arquivo está incorreto
**Solução:** Verificar o caminho e garantir que o arquivo existe

## Como Adicionar Animator ao Modelo (Após Importação Bem-Sucedida)

Se as animações foram importadas corretamente, você precisa usar o `AnimatorComponent`:

```csharp
// 1. Importar modelo
var modelData = await ModelImporter.LoadModelDataAsync("path/to/model.fbx");

// 2. Verificar se tem animações
if (modelData.Animations != null && modelData.Animations.Animations.Count > 0)
{
    Logger.Log($"✓ Modelo tem {modelData.Animations.Animations.Count} animações:");
    foreach (var anim in modelData.Animations.Animations)
    {
        Logger.Log($"  - {anim.Name}: {anim.Duration:F2}s");
    }
    
    // 3. Adicionar Animator ao GameObject
    var animator = new AnimatorComponent();
    animator.AnimationCollection = modelData.Animations;
    gameObject.AddComponent(animator);
    
    // 4. Tocar primeira animação
    animator.Play(0, fade: false);
}
else
{
    Logger.LogWarning("⚠ ModelData não tem animações!");
}
```

## Verificar Logs em Tempo de Execução

Você também pode adicionar logs no seu código para verificar:

```csharp
var modelData = await ModelImporter.LoadModelDataAsync("model.fbx");

// DEBUG: Verificar animações
if (modelData.Animations == null)
{
    Logger.LogError("❌ modelData.Animations é NULL!");
}
else if (modelData.Animations.Animations.Count == 0)
{
    Logger.LogWarning("⚠ modelData.Animations.Animations está vazio!");
}
else
{
    Logger.Log($"✓ {modelData.Animations.Animations.Count} animações importadas:");
    foreach (var anim in modelData.Animations.Animations)
    {
        Logger.Log($"  '{anim.Name}' ({anim.Duration:F2}s) - {anim.Channels.Count} canais");
    }
}
```

## Testar com Modelo do Mixamo

Para testar, baixe um modelo animado do Mixamo:
1. Vá em https://www.mixamo.com
2. Escolha um personagem
3. Escolha 1-2 animações
4. Download em formato FBX
5. Importe no editor

Os modelos do Mixamo são garantidos de ter animações e funcionam bem com Assimp.

## Ainda Não Funciona?

Se após verificar os logs ainda não funcionar:
1. Cole os logs aqui
2. Informe qual arquivo FBX você está usando
3. Descreva de onde veio o arquivo (Blender, Maya, Mixamo, etc.)

Isso ajudará a identificar o problema específico!
