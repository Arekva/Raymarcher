﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raymarcher
{
    class Sphere : Primitive
    {
        public Colour32 Colour;
        public static Sphere Main;
        public Sphere() : base()
        {
            Main = this;
            Colour = new Colour32(EngineInitializer.r.Next(256), EngineInitializer.r.Next(256), EngineInitializer.r.Next(256), 255);
        }

        public override double DistanceFromSurface(Vector3D position)
        {
            return Vector3D.Distance(position, this.Position) - (this.Scale.z * 0.5D);
        }

        public override Vector3D GetNormal(Vector3D point)
        {
            //Converts sphere as unit sphere
            return (point - this.Position).Normalize();
        }

        public override double DistanceFromSurfaceSquared(Vector3D position)
        {
            return Vector3D.DistanceSquared(position, this.Position) - (this.Scale.z * this.Scale.z * 0.5D);
        }

    }
}
