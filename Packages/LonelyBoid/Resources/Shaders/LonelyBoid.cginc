#ifndef LONELY_BOID_CGINC
#define LONELY_BOID_CGINC

#include "SimplexNoise.cginc"

#if SHADER_API_GLCORE || SHADER_API_VULKAN
#define CONST_MEMBER
#else
#define CONST_MEMBER const
#endif

namespace Lonely
{
    static const float PI = 3.141592653589793f;
    static const float SMALL = 1.e-4f;

    struct TurbulentForce
    {
        float spatialScale;
        float temporalScale;

        float2 compute(const float2 position, const float time) CONST_MEMBER
        {
            return SimplexNoise::simplex_noise(
                float3(position.x * spatialScale, position.y * spatialScale, time * temporalScale)).xy;
        }
    };

    struct RadialForce
    {
        float falloffPower;

        float2 compute(const float2 position, const float time) CONST_MEMBER
        {
            return normalize(position) * pow(length(position), falloffPower);
        }
    };

    namespace IO
    {
        struct FlockConfigData
        {
            float2 origin;

            float spawnAtRadius;
            float killAtRadius;

            float viewRadius;
            float viewAngleTau;

            float avoidRadius;
            float avoidAngleTau;

            float minSpeed;
            float maxSpeed;
            float maxAcceleration;
            float maxAngularSpeedTau;
        };

        struct FlockDrivesData
        {
            float alignment;
            float cohesion;
            float separation;
        };

        struct BoidData
        {
            int flockIndex;
            float2 position;
            float2 direction;
            float speed;
        };

        struct ForceData
        {
            float2 origin;
            int type;
            float intensity;

            TurbulentForce turbulent;
            RadialForce radial;
        };
    }

    namespace ForceType
    {
        static const int Radial = 1;
        static const int Turbulence = 2;
    };

    struct Force
    {
        int _type;
        float2 _origin;
        float _intensity;

        TurbulentForce _turbulent;
        RadialForce _radial;

        static Force make(const IO::ForceData data)
        {
            Force f;
            f._type = data.type;
            f._origin = data.origin;
            f._intensity = data.intensity;
            f._radial = data.radial;
            f._turbulent = data.turbulent;
            return f;
        }

        float2 compute(float2 position, const float time) CONST_MEMBER
        {
            position -= _origin;
            float2 force;

            switch (_type)
            {
            case ForceType::Radial:
                force = _radial.compute(position, time);
                break;
            case ForceType::Turbulence:
                force = _turbulent.compute(position, time);
                break;
            default:
                force = float2(0, 0);
                break;
            }

            return force * _intensity;
        }
    };

    void safe_normalize(const float2 v, out float2 dir, out float len)
    {
        len = length(v);
        dir = len < SMALL ? v : v / len;
    }

    float2 safe_normalize(const float2 v)
    {
        float2 dir;
        // ReSharper disable once CppEntityAssignedButNoRead
        float len;
        // ReSharper disable once CppAssignedValueIsNeverUsed
        safe_normalize(v, dir, len);
        return dir;
    }

    struct Boid
    {
        float2 _position;
        float2 _direction;
        float _speed;

        static Boid make(const IO::BoidData data)
        {
            Boid b;
            b._position = data.position;
            b._direction = data.direction;
            b._speed = data.speed;
            return b;
        }

        static Boid make(const float2 position, const float2 velocity)
        {
            Boid b;
            b._position = position;
            safe_normalize(velocity, b._direction, b._speed);
            return b;
        }

        float2 position() CONST_MEMBER
        {
            return _position;
        }

        float2 direction() CONST_MEMBER
        {
            return _direction;
        }

        float speed() CONST_MEMBER
        {
            return _speed;
        }

        float2 velocity() CONST_MEMBER
        {
            return _speed * _direction;
        }

        // ReSharper disable once CppMemberFunctionMayBeConst
        void setVelocity(const float2 v)
        {
            safe_normalize(v, _direction, _speed);
        }

        void setPosition(const float2 pos)
        {
            _position = pos;
        }

        void exportTo(inout IO::BoidData data) CONST_MEMBER {
            data.direction = direction();
            data.position = position();
            data.speed = speed();
        }
    };

    struct SectorCoords
    {
        float distance;
        float cosine;

        static SectorCoords make(const float2 refPosition, const float2 refDirection, const float2 position,
                                 out float2 direction)
        {
            SectorCoords sc;
            safe_normalize(position - refPosition, direction, sc.distance);
            sc.cosine = dot(refDirection, direction);
            return sc;
        }

        static SectorCoords make(const Boid reference, const Boid boid, out float2 direction)
        {
            return make(reference.position(), reference.direction(), boid.position(), direction);
        }
    };

    struct Sector
    {
        float _angleCos;
        float _radius;

        static Sector make(const float radius, const float angle)
        {
            Sector s;
            s._angleCos = cos(angle * 0.5f);
            s._radius = radius;
            return s;
        }

        bool contains(const SectorCoords sectCoords) CONST_MEMBER
        {
            return sectCoords.distance <= _radius && sectCoords.cosine >= _angleCos;
        }
    };


    float cross2(float2 v, float2 w)
    {
        return v.x * w.y - v.y * w.x;
    }

    struct Flock
    {
        float2 _origin;

        float _spawnRadius;
        float _killRadius;

        Sector _viewRange;
        Sector _avoidRange;

        float _minSpeed;
        float _maxSpeed;
        float _maxAcceleration;
        float _maxAngularSpeedTau;

        Sector viewRange() CONST_MEMBER
        {
            return _viewRange;
        }

        Sector avoidRange() CONST_MEMBER
        {
            return _avoidRange;
        }

        static Flock make(const IO::FlockConfigData config)
        {
            Flock f;
            f._origin = config.origin;
            f._spawnRadius = config.spawnAtRadius;
            f._killRadius = config.killAtRadius;
            f._viewRange = Sector::make(config.viewRadius, config.viewAngleTau * 2.f * PI);
            f._avoidRange = Sector::make(config.avoidRadius, config.avoidAngleTau * 2.f * PI);
            f._minSpeed = config.minSpeed;
            f._maxSpeed = config.maxSpeed;
            f._maxAcceleration = config.maxAcceleration;
            f._maxAngularSpeedTau = config.maxAngularSpeedTau;
            return f;
        }

        float2 computeSurvivalDrive(const Boid boid) CONST_MEMBER
        {
            float2 direction;
            float radius;
            safe_normalize(boid.position() - _origin, direction, radius);

            // Go quadratically from 0 at spawnRadius to 1 at killRadius
            const float magnitude = pow(saturate((radius - _spawnRadius) / (_killRadius - _spawnRadius)), 2.f);

            return direction * magnitude;
        }

        float2 clampVelocity(const float2 velocity) CONST_MEMBER {
            float inMagnitude;
            float2 direction;
            safe_normalize(velocity, direction, inMagnitude);
            return direction * clamp(inMagnitude, _minSpeed, _maxSpeed);
        }


        float2 computeVelocityDelta(const Boid boid, const float deltaTime, const float2 acceleration) CONST_MEMBER
        {
            const float maxAngle = _maxAngularSpeedTau * deltaTime * 2.f * PI;
            const float accelMagnitude = length(acceleration);
            float accelFactor = deltaTime;

            if (accelMagnitude >= SMALL)
            {
                /*
                 * Theoretically, the velocity change is given by v += clamp_magnitude(a, 0, maxAcceleration) * deltaT.
                 * This means that the candidates for the velocity change are
                 *  - a * deltaT * maxAcceleration / |a|
                 *  - a * deltaT
                 * whichever is smaller.
                 */
                accelFactor = min(accelFactor, deltaTime * _maxAcceleration / accelMagnitude);

                // Do we need to reduce the acceleration even further to clamp down on angular speed?
                if (maxAngle < PI)
                {
                    // Rotate the current direction by maxAngleRad to obtain the direction that marks the rotation limit
                    const float rotCos = cos(maxAngle);
                    float rotSin = sin(maxAngle);

                    // Does the acceleration make us rotate clockwise or counterclockwise?
                    if (cross2(boid.direction(), acceleration) < 0.f)
                    {
                        rotSin *= -1.f;
                    }

                    const float2 maxAngleDir = float2(
                        boid.direction().x * rotCos - boid.direction().y * rotSin,
                        boid.direction().x * rotSin + boid.direction().y * rotCos
                    );

                    // Intersect velocity + t * acceleration with the line through maxAngleDir.
                    // We calculate the t that yields the intersection.
                    const float aCrossMaxAngleDir = cross2(acceleration, maxAngleDir);

                    // If it's zero or has the opposite sign as rotSin, there is no intersection for a t > 0.
                    // In particular, we will not be exceeding the max angle.
                    if (abs(aCrossMaxAngleDir) > SMALL && sign(aCrossMaxAngleDir) != sign(rotSin))
                    {
                        // Scaling acceleration by this factor will keep the angular rotation under check
                        // I swear this is correct
                        const float scaling = -rotSin * boid.speed() / aCrossMaxAngleDir;
                        accelFactor = min(accelFactor, scaling);
                    }
                }
            }

            return acceleration * accelFactor;
        }
    };

    struct DrivesCalculator
    {
        float2 _cohesion;
        float2 _alignment;
        float2 _separation;
        float _cohesionWeight;
        float _alignmentWeight;
        float _separationWeight;

        Boid _reference;
        Sector _viewRange;
        Sector _avoidRange;

        static DrivesCalculator make(const Flock flock, const Boid reference)
        {
            DrivesCalculator dc;
            dc._cohesion = float2(0, 0);
            dc._alignment = float2(0, 0);
            dc._separation = float2(0, 0);
            dc._cohesionWeight = 0.f;
            dc._alignmentWeight = 0.f;
            dc._separationWeight = 0.f;
            dc._reference = reference;
            dc._viewRange = flock.viewRange();
            dc._avoidRange = flock.avoidRange();
            return dc;
        }

        void update(const Boid boid, const IO::FlockDrivesData flockDrives)
        {
            float2 direction;
            const SectorCoords coords = SectorCoords::make(_reference, boid, direction);

            if (_viewRange.contains(coords))
            {
                _cohesionWeight += flockDrives.cohesion;
                _cohesion += (boid.position() - _reference.position()) * flockDrives.cohesion;

                _alignmentWeight += flockDrives.alignment;
                _alignment += (boid.velocity() - _reference.velocity()) * flockDrives.alignment;
            }

            if (_avoidRange.contains(coords) && coords.distance > SMALL)
            {
                // Other boids contribute inversely proportionally to distance
                _separationWeight += flockDrives.separation;
                _separation -= flockDrives.separation * direction / coords.distance;
            }
        }

        static float2 weightNormalize(float2 v, const float w)
        {
            if (w >= SMALL) v /= w;
            return v;
        }

        void compute(out float2 alignmentDrive, out float2 cohesionDrive, out float2 separationDrive) CONST_MEMBER
        {
            alignmentDrive = weightNormalize(_alignment, _alignmentWeight);
            cohesionDrive = weightNormalize(_cohesion, _cohesionWeight);
            separationDrive = weightNormalize(_separation, _separationWeight);
        }

        float2 total() CONST_MEMBER
        {
            return weightNormalize(_alignment, _alignmentWeight) +
                weightNormalize(_cohesion, _cohesionWeight) +
                weightNormalize(_separation, _separationWeight);
        }
    };
}

#endif
