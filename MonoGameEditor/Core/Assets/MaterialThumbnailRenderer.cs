using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;

namespace MonoGameEditor.Core.Assets;

/// <summary>
/// Generates thumbnail previews of materials using a sphere
/// </summary>
public class MaterialThumbnailRenderer : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private RenderTarget2D? _renderTarget;
    private Model? _sphereModel;
    private Effect? _pbrEffect;
    
    private const int ThumbnailSize = 256;
    
    public MaterialThumbnailRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        InitializeRenderTarget();
    }
    
    private void InitializeRenderTarget()
    {
        _renderTarget = new RenderTarget2D(
            _graphicsDevice,
            ThumbnailSize,
            ThumbnailSize,
            false,
            SurfaceFormat.Color,
            DepthFormat.Depth24
        );
    }
    
    /// <summary>
    /// Generate thumbnail for material
    /// </summary>
    public Texture2D GenerateThumbnail(Materials.PBRMaterial material)
    {
        if (_renderTarget == null)
            throw new InvalidOperationException("RenderTarget not initialized");
        
        // Save current render target
        var oldTargets = _graphicsDevice.GetRenderTargets();
        
        // Set our render target
        _graphicsDevice.SetRenderTarget(_renderTarget);
        _graphicsDevice.Clear(new Color(45, 45, 48)); // Unity background color
        
        // Setup camera for sphere view
        Matrix world = Matrix.CreateRotationY(MathHelper.PiOver4) * Matrix.CreateRotationX(-0.3f);
        Matrix view = Matrix.CreateLookAt(new Vector3(0, 0, 3), Vector3.Zero, Vector3.Up);
        Matrix projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.PiOver4,
            1f, // Square aspect
            0.1f,
            100f
        );
        
        // Render sphere with material
        RenderSphere(material, world, view, projection);
        
        // Restore render target
        _graphicsDevice.SetRenderTargets(oldTargets);
        
        // Create texture from render target
        var thumbnail = new Texture2D(_graphicsDevice, ThumbnailSize, ThumbnailSize);
        Color[] data = new Color[ThumbnailSize * ThumbnailSize];
        _renderTarget.GetData(data);
        thumbnail.SetData(data);
        
        return thumbnail;
    }
    
    private void RenderSphere(Materials.PBRMaterial material, Matrix world, Matrix view, Matrix projection)
    {
        // TODO: Load sphere model and PBR effect
        // For now, just clear with material albedo for testing
        var albedo = material.AlbedoColor;
        _graphicsDevice.Clear(new Color(
            (byte)(albedo.R * 0.5f), 
            (byte)(albedo.G * 0.5f), 
            (byte)(albedo.B * 0.5f)
        ));
    }
    
    /// <summary>
    /// Save thumbnail to PNG file
    /// </summary>
    public void SaveThumbnail(Materials.PBRMaterial material, string outputPath)
    {
        var thumbnail = GenerateThumbnail(material);
        
        // Ensure directory exists
        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Save as PNG
        using (FileStream stream = File.OpenWrite(outputPath))
        {
            thumbnail.SaveAsPng(stream, thumbnail.Width, thumbnail.Height);
        }
        
        thumbnail.Dispose();
    }
    
    public void Dispose()
    {
        _renderTarget?.Dispose();
        // Note: Model doesn't implement IDisposable in MonoGame
        _pbrEffect?.Dispose();
    }
}
