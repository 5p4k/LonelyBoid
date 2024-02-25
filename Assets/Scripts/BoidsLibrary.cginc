#ifndef BOIDS_LIBRARY_CGINC
#define BOIDS_LIBRARY_CGINC

#include <NoiseShader/Packages/jp.keijiro.noiseshader/Shader/SimplexNoise3D.hlsl>

#if SHADER_API_GLCORE || SHADER_API_VULKAN
#define CONST_MEMBER
#else
#define CONST_MEMBER const
#endif

#define PI 3.141592653589793f

float2 safe_normalize(float2 v);

struct boid_data
{
    uint flock_index;
    float2 position;
    float2 direction;
    float speed;

    float2 velocity() CONST_MEMBER
    {
        return speed * direction;
    }

    void set_velocity(const float2 v)
    {
        speed = length(v);
        if (speed > 0.f)
        {
            direction = v / speed;
        }
    }
};

struct flock_data
{
    float view_radius;
    float view_angle_tau;

    float avoid_radius;
    float avoid_angle_tau;

    float separation_weight;
    float alignment_weight;
    float cohesion_weight;
    float survival_weight;

    float max_acceleration;
    float min_speed;
    float max_speed;
    float max_angular_speed_tau;

    float2 center;
    float spawn_radius;
    float kill_radius;
};

struct force_data
{
    uint type;
    float intensity;
    float2 position;
    float falloff_power;
    float spatial_scale;
    float temporal_scale;

    float2 compute_radial_force(const float2 at)
    {
        const float2 radial_vector = at - position;
        const float magnitude = intensity * pow(length(radial_vector), falloff_power);
        return normalize(radial_vector) * magnitude;
    }

    float2 compute_turbulent_force(const float2 at, const float time)
    {
        const float2 location = (at - position) * spatial_scale;
        const float3 input_to_noise = float3(location.x, location.y, time * temporal_scale);
        return intensity * SimplexNoiseGrad(input_to_noise).xy;
    }

    float2 compute(const float2 at, const float time)
    {
        float2 force = float2(0.f, 0.f);
        switch (type)
        {
        case 1:
            force = compute_radial_force(at);
            break;
        case 2:
            force = compute_turbulent_force(at, time);
            break;
        default:
            break;
        }
        return force;
    }
};


float2 clamp_magnitude(float2 v, const float min_magnitude, const float max_magnitude)
{
    const float in_magnitude = length(v);
    const float out_magnitude = clamp(in_magnitude, min_magnitude, max_magnitude);
    if (in_magnitude > 0.f)
    {
        v *= out_magnitude / in_magnitude;
    }
    return v;
}


float2 safe_normalize(float2 v)
{
    const float magnitude = length(v);
    if (magnitude > 0.f)
    {
        v /= magnitude;
    }
    return v;
}

float cross2(float2 v, float2 w)
{
    return v.x * w.y - v.y * w.x;
}


float2 compute_velocity_change(const float delta_time, float2 direction, const float speed, const float2 acceleration,
                               const float max_acceleration, const float max_angle_rad)
{
    const float acceleration_magnitude = length(acceleration);
    float acceleration_factor = delta_time;

    if (acceleration_magnitude >= 1.e-5f)
    {
        /*
         * Theoretically, the velocity change is given by v += clamp_magnitude(a, 0, maxAcceleration) * deltaT.
         * This means that the candidates for the velocity change are
         *  - a * deltaT * maxAcceleration / |a|
         *  - a * deltaT
         * whichever is smaller.
         */
        acceleration_factor = min(acceleration_factor, delta_time * max_acceleration / acceleration_magnitude);

        // Do we need to reduce the acceleration even further to clamp down on angular speed?
        if (max_angle_rad < PI)
        {
            // Rotate the current direction by maxAngleRad to obtain the direction that marks the rotation limit
            const float rot_cos = cos(max_angle_rad);
            float rot_sin = sin(max_angle_rad);

            // Does the acceleration make us rotate clockwise or counterclockwise?
            if (cross2(direction, acceleration) < 0.f)
            {
                rot_sin *= -1.f;
            }

            const float2 max_angle_dir = float2(
                direction.x * rot_cos - direction.y * rot_sin,
                direction.x * rot_sin + direction.y * rot_cos
            );

            // Intersect velocity + t * acceleration with the line through maxAngleDir.
            // We calculate the t that yields the intersection.
            const float a_cross_max_angle_dir = cross2(acceleration, max_angle_dir);

            // If it's zero or has the opposite sign as rotSin, there is no intersection for a t > 0.
            // In particular, we will not be exceeding the max angle.
            if (abs(a_cross_max_angle_dir) > 1.e-5f && sign(a_cross_max_angle_dir) != sign(rot_sin))
            {
                // Scaling acceleration by this factor will keep the angular rotation under check
                // I swear this is correct
                const float scaling = -rot_sin * speed / a_cross_max_angle_dir;
                acceleration_factor = min(acceleration_factor, scaling);
            }
        }
    }

    return acceleration * acceleration_factor;
}

float2 compute_survival_drive(const boid_data boid, const flock_data flock)
{
    float2 to_center_dir = flock.center - boid.position;
    const float radius = length(to_center_dir);

    if (radius > 1.e-5f)
    {
        // If it's that close to the center, the drive will be anyways zero.
        to_center_dir /= radius;
    }

    // Go quadratically from 0 at spawnRadius to 1 at killRadius
    const float magnitude = pow(saturate((radius - flock.spawn_radius) / (flock.kill_radius - flock.spawn_radius)),
                                2.f);

    return to_center_dir * magnitude;
}

struct neighborhood_drives_calculator
{
    /*
     * Refer to [Lebar, Bajec, Zimic, Mraz, 2007]: "The computational beauty of flocking: boids revisited"
     *
     * Note: most other sources, including the original paper by Reynolds, do not deal with the problem of how to
     * computationally update the velocity. This one paper above instead explains exactly how the boids drives are to
     * be implemented into code.
     */

    uint m_mates_count;
    float2 m_mates_mean_position;
    float2 m_mates_mean_velocity;
    float2 m_avoid_potential;

    boid_data m_boid;

    float m_view_angle_cos;
    float m_view_radius;
    float m_avoid_angle_cos;
    float m_avoid_radius;

    void initialize(const boid_data boid, const flock_data flock)
    {
        m_mates_count = 0;
        m_mates_mean_position = float2(0, 0);
        m_mates_mean_velocity = float2(0, 0);
        m_avoid_potential = float2(0, 0);

        m_boid = boid;

        m_view_angle_cos = cos(flock.view_angle_tau * PI);
        m_avoid_angle_cos = cos(flock.avoid_angle_tau * PI);
        m_view_radius = flock.view_radius;
        m_avoid_radius = flock.avoid_radius;
    }

    void update(const boid_data other_boid)
    {
        const float2 boids_offset = other_boid.position - m_boid.position;
        const float boids_distance = length(boids_offset);
        const float2 boids_direction = boids_offset / boids_distance;

        // The dot product of two normal vector is the cosine of the angle between them
        const float angle_cos = dot(boids_direction, m_boid.direction);

        if (other_boid.flock_index == m_boid.flock_index)
        {
            // Same flock, is it in view?
            if (angle_cos > m_view_angle_cos && boids_distance <= m_view_radius)
            {
                // Boids in range and in view that belong to the same flock contribute
                ++m_mates_count;
                m_mates_mean_position += other_boid.position;
                m_mates_mean_velocity += other_boid.velocity();
            }
        }

        // Is in perception for avoidance?
        if (angle_cos > m_avoid_angle_cos && boids_distance > 0.f && boids_distance <= m_avoid_radius)
        {
            // Other boids only contribute to avoidance in a measure equal to 1 / distance
            m_avoid_potential -= boids_direction / boids_distance;
        }
    }

    void compute(out float2 cohesion_drive, out float2 separation_drive, out float2 alignment_drive) CONST_MEMBER
    {
        separation_drive = safe_normalize(m_avoid_potential);

        if (m_mates_count > 0)
        {
            alignment_drive = safe_normalize(m_mates_mean_velocity / m_mates_count - m_boid.velocity());
            cohesion_drive = safe_normalize(m_mates_mean_position / m_mates_count - m_boid.position);
        }
        else
        {
            alignment_drive = float2(0, 0);
            cohesion_drive = float2(0, 0);
        }
    }
};

#endif
