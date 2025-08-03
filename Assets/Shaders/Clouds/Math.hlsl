float remap(float originalValue, float originalMin, float originalMax, float newMin, float newMax)
{
	return newMin + (((originalValue - originalMin) / (originalMax - originalMin)) * (newMax - newMin));
}

bool raySphereIntersection(float3 pos, float3 dir, float3 sphere_c, float sphere_r, out float2 t) {
    float3 l = sphere_c - pos;
    float tca = dot(l, dir);

    float d2 = dot(l, l) - tca * tca;

    if (d2 > sphere_r * sphere_r) {
        return false;
    }

    float thc = sqrt(sphere_r * sphere_r - d2);
    t.x = tca - thc;
    t.y = tca + thc;

    if (t.x > t.y) {
        float temp = t.x;
        t.x = t.y;
        t.y = temp;
    }

    if (t.x < 0.0) {
        t.x = t.y;
        if (t.x < 0.0) {
            return false;
        }
    }

    return true;
}

bool rayPlaneIntersection(float3 n, float3 p0, float3 rayOrigin, float3 rayDir, out float t) {
    float denom = dot(n, rayDir);
    if (denom > 1e-6) {
        float3 p0l0 = p0 - rayOrigin;
        t = dot(p0l0, n) / denom;

        return (t >= 0.0);
    }

    return false;
}

float calculateScatterIntergral(float opticalDepth, float coeff){
    float a = -coeff * (1.0 / log(2.0));
    float b = -1.0 / coeff;
    float c =  1.0 / coeff;

    return exp2(a * opticalDepth) * b + c;
}