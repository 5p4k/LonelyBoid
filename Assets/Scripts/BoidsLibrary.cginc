struct BoidData {
    uint flockIndex;
    float2 position;
    float2 direction;
    float speed;

    float2 velocity() {
        return speed * direction;
    }

    void setVelocity(float2 v) {
        speed = length(v);
        if (speed > 0.f) {
            direction = v / speed;
        }
    }
};

struct FlockData {
    float viewRadius;
    float viewAngleTau;

    float avoidRadius;
    float avoidAngleTau;

    float separationWeight;
    float alignmentWeight;
    float cohesionWeight;
    float survivalWeight;

    float maxAcceleration;
    float minSpeed;
    float maxSpeed;
    float maxAngularSpeedTau;

    float2 center;
    float spawnRadius;
    float killRadius;
};


static const float PI = 3.141592653589793f;


float2 clamp_magnitude(float2 v, float minMagnitude, float maxMagnitude) {
    const float inMagnitude = length(v);
    const float outMagnitude = clamp(inMagnitude, minMagnitude, maxMagnitude);
    if (inMagnitude > 0.f) {
        v *= outMagnitude / inMagnitude;
    }
    return v;
}


float2 safe_normalize(float2 v) {
    const float magnitude = length(v);
    if (magnitude > 0.f) {
        v /= magnitude;
    }
    return v;
}

float cross2(float2 v, float2 w) {
    return v.x * w.y - v.y * w.x;
}


float2 ComputeVelocityChange(float deltaTime, float2 direction, float speed, float2 acceleration,
                             float maxAcceleration, float maxAngleRad) {
    const float accelerationMagnitude = length(acceleration);
    float accelerationFactor = deltaTime;

    if (accelerationMagnitude >= 1.e-5f) {
        /*
         * Theoretically, the velocity change is given by v += clamp_magnitude(a, 0, maxAcceleration) * deltaT.
         * This means that the candidates for the velocity change are
         *  - a * deltaT * maxAcceleration / |a|
         *  - a * deltaT
         * whichever is smaller.
         */
        accelerationFactor = min(accelerationFactor, deltaTime * maxAcceleration / accelerationMagnitude);

        // Do we need to reduce the acceleration even further to clamp down on angular speed?
        if (maxAngleRad < PI) {
            // Rotate the current direction by maxAngleRad to obtain the direction that marks the rotation limit
            const float rotCos = cos(maxAngleRad);
            float rotSin = sin(maxAngleRad);

            // Does the acceleration make us rotate clockwise or counterclockwise?
            if (cross2(direction, acceleration) < 0.f) {
                rotSin *= -1.f;
            }

            const float2 maxAngleDir = float2(
                direction.x * rotCos - direction.y * rotSin,
                direction.x * rotSin + direction.y * rotCos
            );

            // Intersect velocity + t * acceleration with the line through maxAngleDir.
            // We calculate the t that yields the intersection.
            const float aCrossMaxAngleDir = cross2(acceleration, maxAngleDir);

            // If it's zero or has the opposite sign as rotSin, there is no intersection for a t > 0.
            // In particular, we will not be exceeding the max angle.
            if (abs(aCrossMaxAngleDir) > 1.e-5f && sign(aCrossMaxAngleDir) != sign(rotSin)) {
                // Scaling acceleration by this factor will keep the angular rotation under check
                // I swear this is correct
                const float scaling = -rotSin * speed / aCrossMaxAngleDir;
                accelerationFactor = min(accelerationFactor, scaling);
            }
        }
    }

    return acceleration * accelerationFactor;
}

float2 ComputeSurvivalDrive(const BoidData boid, const FlockData flock) {
    float2 toCenterDir = flock.center - boid.position;
    const float radius = length(toCenterDir);

    if (radius > 1.e-5f) {
        // If it's that close to the center, the drive will be anyways zero.
        toCenterDir /= radius;
    }

    // Go quadratically from 0 at spawnRadius to 1 at killRadius
    const float magnitude = pow(saturate((radius - flock.spawnRadius) / (flock.killRadius - flock.spawnRadius)), 2.f);

    return toCenterDir * magnitude;
}

struct NeighborhoodDrivesCalculator {
    /*
     * Refer to [Lebar Bajec, Zimic, Mraz, 2007]: "The computational beauty of flocking: boids revisited"
     *
     * Note: most other sources, including the original paper by Reynolds, do not deal with the problem of how to
     * computationally update the velocity. This one paper above instead explains exactly how the boids drives are to
     * be implemented into code.
     */
 
    uint _matesCount;
    float2 _matesMeanPosition;
    float2 _matesMeanVelocity;
    float2 _avoidPotential;

    BoidData _boid;

    float _viewAngleCos;
    float _viewRadius;
    float _avoidAngleCos;
    float _avoidRadius;

    void Initialize(const BoidData boid, const FlockData flock) {
        _matesCount = 0;
        _matesMeanPosition = float2(0, 0);
        _matesMeanVelocity = float2(0, 0);
        _avoidPotential = float2(0, 0);

        _boid = boid;

        _viewAngleCos = cos(flock.viewAngleTau * PI);
        _avoidAngleCos = cos(flock.avoidAngleTau * PI);
        _viewRadius = flock.viewRadius;
        _avoidRadius = flock.avoidRadius;
    }

    void Update(BoidData otherBoid) {
        const float2 boidsOffset = otherBoid.position - _boid.position;
        const float boidsDistance = length(boidsOffset);
        const float2 boidsDirection = boidsOffset / boidsDistance;

        // The dot product of two normal vector is the cosine of the angle between them
        const float angleCos = dot(boidsDirection, _boid.direction);

        if (otherBoid.flockIndex == _boid.flockIndex) {
            // Same flock, is it in view?
            if (angleCos > _viewAngleCos && boidsDistance <= _viewRadius) {
                // Boids in range and in view that belong to the same flock contribute
                ++_matesCount;
                _matesMeanPosition += otherBoid.position;
                _matesMeanVelocity += otherBoid.velocity();
            }
        }

        // Is in perception for avoidance?
        if (angleCos > _avoidAngleCos && boidsDistance > 0.f && boidsDistance <= _avoidRadius) {
            // Other boids only contribute to avoidance in a measure equal to 1 / distance
            _avoidPotential -= boidsDirection / boidsDistance;
        }
    }

    void Compute(out float2 cohesionDrive, out float2 separationDrive, out float2 alignmentDrive) {
        separationDrive = safe_normalize(_avoidPotential);

        if (_matesCount > 0) {
            alignmentDrive = safe_normalize(_matesMeanVelocity / _matesCount - _boid.velocity());
            cohesionDrive = safe_normalize(_matesMeanPosition / _matesCount - _boid.position);
        } else {
            alignmentDrive = float2(0, 0);
            cohesionDrive = float2(0, 0);
        }
    }
};

