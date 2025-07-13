using UnityEngine;
using UnityEngine.Rendering;

// Custom Mobile Render Pipeline
[CreateAssetMenu(fileName = "MobileRenderPipelineAsset", menuName = "Rendering/Mobile Render Pipeline")]
public class MobileRenderPipelineAsset : RenderPipelineAsset
{
    [Header("Lighting Settings")]
    public bool enableLighting = true;
    public int maxAdditionalLights = 4;
    public bool enableShadows = false;
    
    protected override RenderPipeline CreatePipeline()
    {
        return new MobileRenderPipeline(this);
    }
}