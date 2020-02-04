﻿using Hybridizer.Runtime.CUDAImports;
using rm = Raymarcher.Rendering.RMath;

namespace Raymarcher.Rendering
{
    public sealed class Box : Volume
    {
        public override float Distance(float3 point)
        {
            //TODO: take in count the rotation
            return
                rm.Max(0, HybMath.Abs(point.x - position.x) - scale.x / 2F) * rm.Max(0, HybMath.Abs(point.x - position.x) - scale.x / 2F) +
                rm.Max(0, HybMath.Abs(point.y - position.y) - scale.y / 2F) * rm.Max(0, HybMath.Abs(point.y - position.y) - scale.y / 2F) +
                rm.Max(0, HybMath.Abs(point.z - position.z) - scale.z / 2F) * rm.Max(0, HybMath.Abs(point.z - position.z) - scale.z / 2F);
        }

        public override float3 NormalToSurface(float3 point)
        {
            //TODO: take in count the rotation
            float3 relPos = point - position;
            float3 norm = new float3();
            norm.x = 1;
            norm.y = norm.z = 0;

            if (relPos.y > scale.y / 2F)
            {
                norm.x = 0;
                norm.y = 1;
            }
            else if (relPos.y < -scale.y / 2F)
            {
                norm.x = 0;
                norm.y = -1;
            }
            //defaults
            /*else if (relPos.x > scale.x / 2F)
            {
                norm.x = 1;
            }*/
            else if (relPos.x < -scale.x / 2F)
            {
                norm.x = -1;
            }
            else if (relPos.z > scale.z / 2F)
            {
                norm.x = 0;
                norm.z = 1;
            }
            else if (relPos.z < -scale.z / 2F)
            {
                norm.x = 0;
                norm.z = -1;
            }

            return norm;
        }
    }
}
