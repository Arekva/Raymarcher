#include <quaternion.h>
#include <vector3.h>
#include <color.h>
#include <physics.h>
#include <redef.h>
#include <volume.h>
#include <raymath.h>

typedef struct gpu_camera
{
    int2 resolution;
    float fov;
    vector3 position;
    quaternion rotation;
    vector3 mainDirection;
    float farClipPlane;
    float precision;
    volume volume1;

} camera;

bool rm_render_raymarch(__global volume* volumes, size_t n, ray r, camera cam, hit* h);
ray rm_render_rayDirection(camera cam, int2 pixel);
color rm_render_skyColour(vector3 dir, vector3 sunDir);

__kernel void rm_render_entry(__global camera* input, __global volume* shapes, __global size_t* n, __global byte* output)
{   
    size_t i = get_global_id(0);

    camera cam = *input;
    size_t objectAmount = *n;

    size_t x = i % 1024;
    size_t y = i / 1024;

    int2 pixel;
    pixel.x = x;
    pixel.y = y; 

    ray r = rm_render_rayDirection(cam, pixel);
    color col = rm_render_skyColour(r.direction, cam.mainDirection);

    float sunAltitude = 1.0F;
    bool negative = cam.mainDirection.y < 0.0F;
    sunAltitude = fabs(cam.mainDirection.y);

    const color SunDayColour = rm_color_createFromKnown(white);
    const color SunSetColour = rm_color_createFromRGBA(1.0F, 0.33F, 0.0F, 1.0F);

    if(rm_vector3_squaredDistance(r.direction, cam.mainDirection) < 0.0025F && r.direction.y > 0.0F)
    {
        if(negative)
        {
            col = SunSetColour;
        }

        else
        {
            col = rm_color_lerp(SunSetColour, SunDayColour, sqrt(sunAltitude * 2.0F));
        }
    }

    //size_t arraySize = 1;

    hit hi;
    if(rm_render_raymarch(shapes, objectAmount, r, cam, &hi))
    {
        float angle = rm_vector3_angleDegree(hi.normal, cam.mainDirection);
        float lightIntensity = cos(angle * -1.0F * DEG_TO_RAD);

        color diffuse = rm_color_multiply(rm_color_createFromKnown(white), lightIntensity);
        col = diffuse;

        r.origin = rm_vector3_add(hi.location, rm_vector3_multiply(hi.normal, cam.precision));
        r.direction = rm_vector3_multiply(cam.mainDirection, 1.0F);
        
        hi.hit = false;

        // Shadows
        //if(rm_render_raymarch(chunk, arraySize, r, cam, &hi))
        //{
        //    col = rm_color_createFromKnown(black);
        //}
    }

    

    output[i * 4 + 0] = col.b * 255;    //Blue
    output[i * 4 + 1] = col.g * 255;    //Green
    output[i * 4 + 2] = col.r * 255;    //Red
    output[i * 4 + 3] = col.a * 255;    //Alpha
}

float rm_render_closestDistance(__global volume* volumes, size_t n, vector3 p, int * index)
{
    float closest = INFINITY;

    for (size_t i = 0; i < n; i++)
    {
        float d = rm_volume_squaredDistance(volumes[i], p);
        if(d < closest)
        {
            * index = i;
            closest = d;
        }
    }

    return closest;
}

bool rm_render_raymarch(__global volume* volumes, size_t n, ray r, camera cam, hit* h)
{
    

    vector3 probe = r.origin;
    float rayDst = 0.0F;
    float maxDst = 100.0F;

    bool hasHit = false;
    
    
    float precision = cam.precision;

    while(!hasHit && rayDst < maxDst)
    {
        int closestIndex = 1;
        /*rm_volume_squaredDistance(volumes[0], probe);*/
        float dst = rm_render_closestDistance(volumes, n, probe, &closestIndex);
        if(dst < precision)
        {
            hasHit = true;
            *h = rm_hit_create(probe, rm_volume_normal(volumes[(int)closestIndex], probe));
        }

        probe = rm_vector3_add(probe, rm_vector3_multiply(r.direction, dst));
        rayDst += dst;
    }

    return hasHit;
}

ray rm_render_rayDirection(camera cam, int2 pixel)
{
    // https://www.scratchapixel.com/lessons/3d-basic-rendering/ray-tracing-generating-camera-rays/generating-camera-rays?http://www.scratchapixel.com/lessons/3d-basic-rendering/3d-viewing-pinhole-camera/how-pinhole-camera-works-part-1
    int x = pixel.x;
    int y = pixel.y;

    float imageAspectRatio = cam.resolution.x / (float)cam.resolution.y; // assuming width > height 
    float Px = (2.0F * ((x + 0.5F) / cam.resolution.x) - 1.0F) * tan(cam.fov / 2.0F * F_PI / 180.0F) * imageAspectRatio;
    float Py = (1.0F - 2.0F * ((y + 0.5F) / cam.resolution.y)) * tan(cam.fov / 2.0F * F_PI / 180.0F); 

    vector3 rayOrigin = cam.position;
    vector3 rayDirection = rm_vector3_create(Px, Py, 1.0F);
    
    rayDirection = rm_vector3_normalize(rayDirection);
    rayDirection = rm_quaternion_multiply(cam.rotation, rayDirection);

    return rm_ray_create(rayOrigin, rayDirection);
}

color rm_render_skyColour(vector3 dir, vector3 sunDir)
{
    sunDir = rm_vector3_normalize(sunDir);

    const color HorizonColourDay = rm_color_createFromRGBA(0.82F, 0.92F, 0.98F, 1.0F);
    const color HorizonColourSunset = rm_color_createFromRGBA(1.0F, 0.48F, 0.0F, 1.0F);
    const color NightColour = rm_color_createFromRGBA(0.0F, 0.0F, 0.0F, 1.0F);
    const color HighAtmosphereColour = rm_color_createFromRGBA(0.23F, 0.41F, 0.70F, 1.0F);
    const color GroundAtmosphereColour = rm_color_createFromRGBA(0.58F, 0.53F, 0.45F, 1.0F);

    color pix = rm_color_createFromRGBA(0.0F,0.0F,0.0F,1.0F);

    if(sunDir.x == 0.0F && sunDir.y == 0.0F && sunDir.z == 0.0F)
    {
        return pix;
    }

    float sunAltitude = 1.0F;

    bool negative = sunDir.y < 0.0F;
    sunAltitude = fabs(sunDir.y);

    color DenseAtmosphereColour = rm_color_lerp(HorizonColourSunset, HorizonColourDay, sqrt(sunAltitude));
    color highAtmosphereColour = rm_color_lerp(NightColour, HighAtmosphereColour, pow(sunAltitude, 0.6F));
    color groundAtmosphereColour = rm_color_lerp(NightColour, GroundAtmosphereColour, pow(sunAltitude, 0.6F));

    if(negative)
    {
        DenseAtmosphereColour = rm_color_lerp(HorizonColourSunset, NightColour, sqrt(sunAltitude));
        highAtmosphereColour = NightColour;
        groundAtmosphereColour = NightColour;
    }

    if (dir.y > 0.0F)
    {
        pix = rm_color_lerp(DenseAtmosphereColour, highAtmosphereColour, pow(dir.y, 0.8F));
    }
    else
    {
        pix = rm_color_lerp(DenseAtmosphereColour, groundAtmosphereColour, pow(fabs(dir.y), 0.25F));
    }

    return pix;
}

