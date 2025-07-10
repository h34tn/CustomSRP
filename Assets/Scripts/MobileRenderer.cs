using UnityEngine;
using UnityEngine.Rendering;

// Custom Mobile Render Pipeline
[CreateAssetMenu(fileName = "MobileRenderPipelineAsset", menuName = "Rendering/Mobile Render Pipeline")]
public class MobileRenderPipelineAsset : RenderPipelineAsset
{
    [Header("Lighting Settings")]
    public bool enableLighting = true;
    public int maxDirectionalLights = 1;
    public int maxAdditionalLights = 1;
    public bool enableShadows = false;
    
    protected override RenderPipeline CreatePipeline()
    {
        return new MobileRenderPipeline(this);
    }
}

public class MobileRenderPipeline : RenderPipeline
{
    private MobileRenderPipelineAsset pipelineAsset;
    
    // Shader property IDs
    private static readonly int _LightColor = Shader.PropertyToID("_LightColor");
    private static readonly int _LightDirection = Shader.PropertyToID("_LightDirection");
    private static readonly int _LightPosition = Shader.PropertyToID("_LightPosition");
    private static readonly int _LightCount = Shader.PropertyToID("_LightCount");
    private static readonly int _AmbientLight = Shader.PropertyToID("_AmbientLight");
    
    public MobileRenderPipeline(MobileRenderPipelineAsset asset)
    {
        pipelineAsset = asset;
        
        // Set global render pipeline settings
        GraphicsSettings.useScriptableRenderPipelineBatching = true;
    }
    
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras)
        {
            RenderCamera(context, camera);
        }
    }

    void RenderCamera(ScriptableRenderContext context, Camera camera)
    {
        // Set up camera
        context.SetupCameraProperties(camera);

        // Clear
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "Clear";
        cmd.ClearRenderTarget(true, true, camera.backgroundColor);
        context.ExecuteCommandBuffer(cmd);
        cmd.Dispose();

        // Cull objects
        if (!camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParams))
            return;

        CullingResults cullingResults = context.Cull(ref cullingParams);

        // Setup lighting
        if (pipelineAsset.enableLighting)
        {
            SetupLighting(context, cullingResults);
        }

        // Render opaque objects
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        
        // Support both lit and unlit shaders
        var drawingSettings = new DrawingSettings(new ShaderTagId("UniversalForward"), sortingSettings);
        drawingSettings.SetShaderPassName(1, new ShaderTagId("SRPDefaultUnlit"));
        drawingSettings.SetShaderPassName(2, new ShaderTagId("LightweightForward"));
        
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        // Render skybox
        context.DrawSkybox(camera);

        // Render transparent objects
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        // Submit
        context.Submit();
    }
    
    void SetupLighting(ScriptableRenderContext context, CullingResults cullingResults)
    {
        CommandBuffer lightCmd = new CommandBuffer();
        lightCmd.name = "Setup Lighting";
        
        // Setup ambient lighting
        lightCmd.SetGlobalVector(_AmbientLight, RenderSettings.ambientLight);
        
        // Get visible lights
        var visibleLights = cullingResults.visibleLights;
        int lightCount = 0;
        
        // Arrays for light data
        Vector4[] lightColors = new Vector4[pipelineAsset.maxAdditionalLights];
        Vector4[] lightDirections = new Vector4[pipelineAsset.maxAdditionalLights];
        Vector4[] lightPositions = new Vector4[pipelineAsset.maxAdditionalLights];
        
        // Process directional lights first
        for (int i = 0; i < visibleLights.Length && lightCount < pipelineAsset.maxAdditionalLights; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            Light light = visibleLight.light;
            
            if (light.type == LightType.Directional)
            {
                // Main directional light (sun)
                Vector4 lightDir = -light.transform.forward;
                Vector4 lightColor = light.color * light.intensity;
                
                lightCmd.SetGlobalVector("_MainLightDirection", lightDir);
                lightCmd.SetGlobalVector("_MainLightColor", lightColor);
                
                // Add to additional lights array
                lightDirections[lightCount] = lightDir;
                lightColors[lightCount] = lightColor;
                lightPositions[lightCount] = Vector4.zero; // Not used for directional
                lightCount++;
            }
        }
        
        // Process point and spot lights
        for (int i = 0; i < visibleLights.Length && lightCount < pipelineAsset.maxAdditionalLights; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            Light light = visibleLight.light;
            
            if (light.type == LightType.Point || light.type == LightType.Spot)
            {
                Vector4 lightPos = light.transform.position;
                lightPos.w = 1.0f / (light.range * light.range); // Attenuation
                
                Vector4 lightColor = light.color * light.intensity;
                Vector4 lightDir = -light.transform.forward;
                
                lightPositions[lightCount] = lightPos;
                lightColors[lightCount] = lightColor;
                lightDirections[lightCount] = lightDir;
                lightCount++;
            }
        }
        
        // Set light arrays to shaders
        lightCmd.SetGlobalInt(_LightCount, lightCount);
        lightCmd.SetGlobalVectorArray(_LightColor, lightColors);
        lightCmd.SetGlobalVectorArray(_LightDirection, lightDirections);
        lightCmd.SetGlobalVectorArray(_LightPosition, lightPositions);
        
        context.ExecuteCommandBuffer(lightCmd);
        lightCmd.Dispose();
    }
}