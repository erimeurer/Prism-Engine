using MonoGameEditor.Core.Components;
using Microsoft.Xna.Framework;

public class NewScript : ScriptComponent
{
    public override string ComponentName => "New Script";
    
    public override void Start()
    {
        // Called when the script is first initialized
    }
    
    public override void Update(GameTime gameTime)
    {
        // Called every frame
        // Example: Rotate the object
        // transform.LocalRotation += new Vector3(0, 1, 0) * (float)gameTime.ElapsedGameTime.TotalSeconds;
    }
}
