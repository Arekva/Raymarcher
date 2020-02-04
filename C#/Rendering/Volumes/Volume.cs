﻿using Hybridizer.Runtime.CUDAImports;

namespace Raymarcher.Rendering
{
    public abstract class Volume
    {
        public float3 position;
        public float3 scale;
        public float4 rotation;

        public abstract float Distance(float3 point);
        public float SqrtDistance(float3 point)
        {
            return HybMath.Sqrt(Distance(point));
        }

        public abstract float3 NormalToSurface(float3 point);
    }
}
