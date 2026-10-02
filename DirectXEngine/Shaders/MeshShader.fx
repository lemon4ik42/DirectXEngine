
struct VS_IN
{
    float3 position : POSITION;
    float3 normal : NORMAL;
    float3 biNormal : BINORMAL;
    float3 tangent : TANGENT;
    float2 uvs : UV;
};

struct PS_IN
{
    float4 position : SV_POSITION;
    float4 worldPosition : TEXCOORD0;
    float3 normal : TEXCOORD1;
    float3 normalWorldDirection : TEXCOORD2;
    float3 biNormal : TEXCOORD3;
    float3 tangent : TEXCOORD4;
    float2 uvs : TEXCOORD5;
    float2 pixelUV : TEXCOORD6;
};

struct BaseLightInput
{
    float4 color;
    int castSwadows;
    int multiSampling;
    float shadowMapSampleSize;
};

struct FaceMatrix
{
    float4x4 viewProjection;
    float3 direction;
};

struct PointLight : BaseLightInput
{
    float3 worldPosition;
    float range;
    float diffusion;
    float intensity;
    int startTextureIndex;
    FaceMatrix faceMatrices[6];
};

struct DirectionalLight : BaseLightInput
{
    float3 direction;
    float farClipPlane;
    int startTextureIndex;
    int texturesCount;
    float maxShadowCastDistance;
    float frustumLength;
    float4x4 viewProjectionMatrices[10];
    float2 shadowMapsResolutionFactor[10];
};

struct Spotlight : BaseLightInput
{
    float3 position;
    float3 direction;
    float angle;
    float range;
    float4x4 viewProjection;
};

struct LightData
{
    float3 color;
    float brightness;
};

cbuffer Material : register(b0)
{
    float4 Color;
    float Diffuse;
    float Specular;
    float SpecularShininess;
};

cbuffer ConstantBuffer : register(b1)
{
    float4x4 ModelViewProjection;
    float4x4 ModelLocalToWorldDirection;
    float4x4 ModelLocalToWorld;
    float3 CameraPosition;
    float GlobalIllumination;
    int IsHaveTexture;
    int HasHeightMap;
};

StructuredBuffer<PointLight> PointLights : register(t0);
StructuredBuffer<DirectionalLight> DirectionalLights : register(t1);
StructuredBuffer<Spotlight> Spotlights : register(t2);
Texture2DArray<float> PointLigtsShadowMap : register(t3);
Texture2DArray<float> DirectionalLigtsShadowMap : register(t4);
Texture2DArray<float> SpotlightsShadowMap : register(t5);
Texture2D Texture : register(t6);
Texture2D FrameTexture : register(t7);
Texture2D HeightMap : register(t8);
SamplerState Sampler : register(s0);
SamplerState TextureSampler : register(s1);
SamplerState HeightMapSampler : register(s2);
SamplerState AlphaSampler
{
    Filter = MIN_MAG_MIP_POINT;
    AddressU = BORDER;
    AddressV = BORDER;
    AddressW = BORDER;
    ComparisonFunc = NEVER;
    BorderColor = float4(1.0, 0.0, 0.0, 1.0);
};

bool IntToBool(int value)
{
    return value == 1;
}

uint GetPointLightsCount()
{
    uint count;
    uint stride;
    
    PointLights.GetDimensions(count, stride);
    
    return count;
}

uint GetDirectionalLightsCount()
{
    uint count;
    uint stride;
    
    DirectionalLights.GetDimensions(count, stride);
    
    return count;
}

uint GetSpotightsCount()
{
    uint count;
    uint stride;
    
    Spotlights.GetDimensions(count, stride);
    
    return count;
}

float CosAngle(float3 vector1, float3 vector2)
{
    float length1 = length(vector1);
    float length2 = length(vector2);
    float lengthProduct = length1 * length2;
    
    if (lengthProduct == 0)
        return 1;
    
    return dot(vector1, vector2) / lengthProduct;
}

float3 Reflect(float3 vec, float3 normal)
{
    float3 normalizedNormal = normalize(normal);
    float dotProduct = dot(vec, normalizedNormal);
    float3 projection = normalizedNormal * (dotProduct * 2);
    return vec - projection;
}

bool IsPixelPositionValid(float2 pixelPosition)
{
    float2 absPixelPosition = abs(pixelPosition);
    return absPixelPosition.x <= 1 && absPixelPosition.y <= 1;
}

float3 CalculateUV(float4x4 viewProjection, float4 worldPosition, bool transformZ = false, float range = 0)
{
    float4 prjectionPosition = mul(viewProjection, worldPosition);
    float w = prjectionPosition.w;
    float2 uv = prjectionPosition.xy / w;
    uv.x = (uv.x + 1) / 2;
    uv.y = (-uv.y + 1) / 2;
    
    float z = 0;

    if (transformZ)
        z = w / range;
    else
        z = prjectionPosition.z / w;
    
    return float3(uv, z);
}

float2 ProjectionToUV(float2 projection)
{
    float2 uv = float2(0, 0);
    uv.x = (projection.x + 1) / 2;
    uv.y = (-projection.y + 1) / 2;
    return uv;
}

float CalculateLightPercent(Texture2DArray<float> shadowMap, uint index, float2 pixelPosition, float pixelDepth, float squareSize, uint squarePixelsSize, 
    bool skipValidation = false)
{
    if (!skipValidation && !IsPixelPositionValid(pixelPosition))
        return 1;
    
    uint width;
    uint height;
    uint elements;
    
    shadowMap.GetDimensions(width, height, elements);
    
    float pixelDistanceX = squareSize / (float)squarePixelsSize;
    float pixelDistanceY = squareSize / (float)squarePixelsSize;
    
    float halfWidth = squareSize / 2;
    float halfHeight = squareSize / 2;
    
    float2 currentPixelPosition = pixelPosition - float2(halfWidth, halfHeight);
    float startX = currentPixelPosition.x;
    
    float inShadowPixelsCount = 0;
    float samplesCount = 0;
    
    for (uint row = 0; row < squarePixelsSize; row++)
    {
        for (uint column = 0; column < squarePixelsSize; column++)
        {
            if (!IsPixelPositionValid(currentPixelPosition))
            {
                currentPixelPosition.x += pixelDistanceX;
                continue;
            }
            
            float lightDepth = shadowMap.SampleLevel(Sampler, float3(currentPixelPosition, index), 0);
            
            if (pixelDepth > lightDepth && lightDepth != 1)
                inShadowPixelsCount++;
            
            currentPixelPosition.x += pixelDistanceX;
            samplesCount++;
        }
        
        currentPixelPosition.x = startX;
        currentPixelPosition.y += pixelDistanceY;
    }
    
    float shadowPercent = inShadowPixelsCount / samplesCount;
    return 1 - shadowPercent;
}

float CalculateLightBrightness(PS_IN input, float3 direction)
{
    float3 normal = input.normalWorldDirection;
    float diffuseBrightness = saturate(-CosAngle(direction, normal));
    
    float3 reflectedDirection = Reflect(direction, normal);
    float3 cameraLocal = CameraPosition - input.worldPosition.xyz;
    float specularBrightness = saturate(CosAngle(reflectedDirection, cameraLocal));
    
    float brightness = (diffuseBrightness * Diffuse) + pow(specularBrightness * Specular, SpecularShininess);
    
    return saturate(brightness);
}

float3 MixColors(float3 color1, float3 color2)
{
    return color1 * color2;
}

float3 CalulateAverageColor(float3 color1, float3 color2)
{
    return (color1 + color2) / 2;
}

float3 AddColor(float3 referenceColor, float3 colorToAdd, float brightness)
{
    return lerp(referenceColor, colorToAdd, brightness);
}

float SamplePointLight(PointLight light, float4 pixelWorldPosition)
{
    float3 pixelLocal = pixelWorldPosition.xyz - light.worldPosition;
    
    [unroll(6)]
    for (int i = 0; i < 6; i++)
    {
        FaceMatrix faceMatrix = light.faceMatrices[i];
        
        float3 UV = CalculateUV(faceMatrix.viewProjection, pixelWorldPosition, true, light.range);
        
        uint index = light.startTextureIndex + i;
        float lightPercent = CalculateLightPercent(PointLigtsShadowMap, index, UV.xy, UV.z, light.shadowMapSampleSize, light.multiSampling);
        
        if (lightPercent < 1)
            return lightPercent;
    }
    
    return 1;
}

LightData GetPixelColorForPointLights(PS_IN input)
{
    uint lightsCount = GetPointLightsCount();
    float3 color = float3(0, 0, 0);
    float brightness = 0;
    float4 pixelWorldPosition = input.worldPosition;
    
    for (uint i = 0; i < lightsCount; i++)
    {
        PointLight light = PointLights[i];
        
        float3 lightLocal = light.worldPosition - pixelWorldPosition.xyz;
        float distanceToLight = length(lightLocal);
        float range = light.range;
        
        if (distanceToLight > range)
            continue;
        
        float diffusion = light.diffusion;
        float intensity = light.intensity;
        
        float3 lightDirection = pixelWorldPosition.xyz - light.worldPosition;
        float currentBrightness = CalculateLightBrightness(input, lightDirection);
        currentBrightness = (currentBrightness + diffusion) / (1 + diffusion);
        
        currentBrightness *= intensity;
        
        float rangeSquared = range * range;
        float distanceSquared = distanceToLight * distanceToLight;
        
        float distanceCoefficient = (rangeSquared - distanceSquared) / rangeSquared;
        
        currentBrightness *= distanceCoefficient;
        
        if (!IntToBool(light.castSwadows))
        {
            currentBrightness = max(currentBrightness, GlobalIllumination);
            brightness = max(brightness, currentBrightness);
            color = AddColor(color, light.color.xyz, currentBrightness);
            continue;
        }
        
        float lightPercent = SamplePointLight(light, pixelWorldPosition);
        
        currentBrightness *= lightPercent;
        currentBrightness = max(currentBrightness, GlobalIllumination);
        brightness = max(brightness, currentBrightness);
        
        color = AddColor(color, light.color.xyz, currentBrightness);
    }
    
    LightData data;
    data.color = color;
    data.brightness = brightness;
    
    return data;
}

float SampleDirectionalLight(DirectionalLight light, float4 pixelWorldPosition)
{
    [loop]
    for (int i = 0; i < light.texturesCount; i++)
    {
        float3 UV = CalculateUV(light.viewProjectionMatrices[i], pixelWorldPosition);
        UV.xy = UV.xy * light.shadowMapsResolutionFactor[i];
        
        uint index = light.startTextureIndex + i;
        
        float lightPercent = CalculateLightPercent(DirectionalLigtsShadowMap, index, UV.xy, UV.z, light.shadowMapSampleSize, light.multiSampling);
        
        if (lightPercent < 1)
            return lightPercent;
    }
    
    return 1;
}

float GetSingleDirectionalLightBrightness(DirectionalLight light, PS_IN input, uint index)
{
    float brightness = CalculateLightBrightness(input, light.direction);
    
    if (!IntToBool(light.castSwadows))
        return max(brightness, GlobalIllumination);
    
    return max(brightness * SampleDirectionalLight(light, input.worldPosition), GlobalIllumination);
}

LightData GetDirectionalLightsColor(PS_IN input)
{
    uint lightsCount = GetDirectionalLightsCount();
    float3 color = float3(0, 0, 0);
    float brightness = 0;
    
    for (uint i = 0; i < lightsCount; i++)
    {
        DirectionalLight light = DirectionalLights[i];
        float currentBrightness = GetSingleDirectionalLightBrightness(light, input, i);
        
        brightness = max(brightness, currentBrightness);
        
        color = AddColor(color, light.color.xyz, currentBrightness);
    }

    LightData data;
    
    data.color = color;
    data.brightness = brightness;
    
    return data;
}

float GetSingleSpotlightsBrightness(Spotlight light, PS_IN input, uint index)
{
    float3 lightLocal = input.worldPosition.xyz - light.position;
    float cosLightAngle = CosAngle(lightLocal, light.direction);
    float lightAngle = acos(cosLightAngle);
    
    if (lightAngle > light.angle)
        return 0;
    
    float distanceToLight = distance(light.position, input.worldPosition.xyz);
    float maxDistanceToLight = light.range / cosLightAngle;
    
    if (distanceToLight > maxDistanceToLight)
        return 0;
    
    float brightness = -CosAngle(lightLocal, input.normalWorldDirection);
    
    if (brightness < 0 || !IntToBool(light.castSwadows))
        return 0;
    
    float3 UV = CalculateUV(light.viewProjection, input.worldPosition, true, light.range);
    float lightPercent = CalculateLightPercent(SpotlightsShadowMap, index, UV.xy, UV.z, light.shadowMapSampleSize, light.multiSampling);
    
    brightness *= (90 - lightAngle) / 90;
    brightness *= (maxDistanceToLight - distanceToLight) / maxDistanceToLight;
    
    return brightness * lightPercent;
}

float3 GetSpotlightsColor(PS_IN input)
{
    uint lightsCount = GetSpotightsCount();
    float3 color = float3(0, 0, 0);
    
    for (uint i = 0; i < lightsCount; i++)
    {
        Spotlight light = Spotlights[i];
        color = AddColor(color, light.color.xyz, GetSingleSpotlightsBrightness(light, input, i));
    }
    
    return color;
}

float CalculateColorBrightness(float3 color)
{
    return (color.x + color.y + color.z) / 3;
}

float3 GetPixelColor(PS_IN input)
{
    uint pointLightsCount = GetPointLightsCount();
    uint directionalLightsCount = GetDirectionalLightsCount();
    uint spotlightsCount = GetSpotightsCount();
    uint lightsCount = pointLightsCount + directionalLightsCount + spotlightsCount;
    
    if (lightsCount == 0)
        return float3(GlobalIllumination, GlobalIllumination, GlobalIllumination);
    
    float3 color = float3(0, 0, 0);
    
    if (pointLightsCount != 0)
    {
        LightData pointLightsData = GetPixelColorForPointLights(input);
        color = AddColor(color, pointLightsData.color, pointLightsData.brightness);
    }
    if (directionalLightsCount != 0)
    {
        LightData directionalLightsData = GetDirectionalLightsColor(input);
        color = AddColor(color, directionalLightsData.color, directionalLightsData.brightness);
    }
    if (spotlightsCount != 0)
    {
        float3 spotLightsColor = GetSpotlightsColor(input);
        color = AddColor(color, spotLightsColor, CalculateColorBrightness(spotLightsColor));
    }
    
    return color;
}

float3 CalculatePerpendicular(float3 normal)
{
    if (normal.z == 0)
        return float3(1, 1, 0);

    float z = (-normal.x - normal.y) / normal.z;
    
    return float3(1, 1, z);
}

PS_IN VS(VS_IN input)
{
    PS_IN output;
    
    float4 inputPosition = float4(input.position.xyz, 1);
    float4 inputWorldPosition = mul(ModelLocalToWorld, inputPosition);
    
    output.worldPosition = inputWorldPosition;
    output.position = mul(ModelViewProjection, inputPosition);
    output.normal = input.normal;
    
    float3x3 localToWorldDirection = (float3x3)ModelLocalToWorldDirection;
    
    float3 normal = mul(localToWorldDirection, input.normal);
    float3 biNormal = mul(localToWorldDirection, CalculatePerpendicular(normal));
    
    output.normalWorldDirection = normal;
    output.biNormal = mul(localToWorldDirection, CalculatePerpendicular(normal)); //normalize(mul(localToWorldDirection, input.biNormal));
    output.tangent = cross(biNormal, normal); //normalize(mul(localToWorldDirection, input.tangent));
    output.uvs = input.uvs;
    output.pixelUV = ProjectionToUV(output.position.xy / output.position.w);    
    
    return output;
}

float ClampColorComponent(float current, float reference)
{
    if (reference == 0)
        return 0;

    if (current / reference < GlobalIllumination)
        return reference * GlobalIllumination;

    return current;
}

float3 CalculateHeightMapNormal(PS_IN input)
{
    uint width;
    uint height;
    
    HeightMap.GetDimensions(width, height);
    
    float2 uv = input.uvs;
    float currentHeight = HeightMap.Sample(HeightMapSampler, uv).x;
    
    float pixelDistanceX = (1 / (float) width) * 5;
    float pixelDistanceY = (1 / (float) height) * 5;
    
    float upHeight = HeightMap.Sample(HeightMapSampler, float2(uv.x, uv.y + pixelDistanceY)).x * 2;
    float rightHeight = HeightMap.Sample(HeightMapSampler, float2(uv.x + pixelDistanceX, uv.y)).x * 2;
    float downHeight = HeightMap.Sample(HeightMapSampler, float2(uv.x, uv.y - pixelDistanceY)).x * 2;
    float leftHeight = HeightMap.Sample(HeightMapSampler, float2(uv.x - pixelDistanceX, uv.y)).x * 2;
    
    float3 worldPosition = input.worldPosition.xyz;
    float3 normal = normalize(input.normalWorldDirection);
    float3 biNormal = normalize(input.biNormal);
    float3 tangent = normalize(input.tangent);
    
    float3 currentPosition = worldPosition + (normal * currentHeight);
    
    float3 upPosition = worldPosition + biNormal + (normal * upHeight);
    float3 rightPosition = worldPosition + tangent + (normal * rightHeight);
    float3 downPosition = worldPosition - biNormal + (normal * downHeight);
    float3 leftPosition = worldPosition + tangent + (normal * leftHeight);
    
    float3 cross1 = normalize(cross(rightPosition - currentPosition, upPosition - currentPosition));
    float3 cross2 = normalize(cross(downPosition - currentPosition, rightPosition - currentPosition));
    float3 cross3 = normalize(cross(leftPosition - currentPosition, downPosition - currentPosition));
    float3 cross4 = normalize(cross(leftPosition - currentPosition, upPosition - currentPosition));   
    
    return (cross1 + cross2 + cross3 + cross4) / 4;
}

float3 PS(PS_IN input) : SV_Target
{
    if (IntToBool(HasHeightMap))
    {
        input.normalWorldDirection = CalculateHeightMapNormal(input);
        //input.normalWorldDirection += CalculateHeightMapNormal(input);
    }
    
    float3 lightColor = GetPixelColor(input);
    float3 referenceColor = Color.xyz;
    float3 color = referenceColor;
    float a = Color.w;
    
    if (IntToBool(IsHaveTexture))
    {
        float4 textureColor = Texture.Sample(TextureSampler, input.uvs);
        referenceColor = textureColor.xyz;
        color = referenceColor;
        a = textureColor.w;
    }
    
    color *= lightColor;
    
    color.x = ClampColorComponent(color.x, referenceColor.x);
    color.y = ClampColorComponent(color.y, referenceColor.y);
    color.z = ClampColorComponent(color.z, referenceColor.z);
    
    if (a == 1)
        return color;
    
    float3 frameColor = FrameTexture.Sample(AlphaSampler, input.pixelUV);
    
    color = AddColor(frameColor, color, a);   
    
    return color;
}