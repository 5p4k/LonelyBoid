using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


public class Flock : MonoBehaviour
{
    [Header("Spawn")]
    public Boid prefab;
    public float spawnFrequency = 3.0f;
    public float spawnRadius = 5.0f;
    public uint maxCount = 40;
    public float killRadius = 13.0f;

    [Header("Behaviour")]
    public float separationWeight = 2.0f;
    public float alignmentWeight = 3.0f;
    public float cohesionWeight = 4.0f;
    public float survivalWeight = 10.0f;

    [Header("Dynamics")]
    public float maxAcceleration = 2.0f;
    public float minSpeed = 0.1f;
    public float maxSpeed = 3.0f;
    public float maxAngularSpeedTau = 0.5f;

    [Header("Perception")]
    public float viewRadius = 4.0f;
    public float viewAngleTau = 0.5f;
    public float avoidRadius = 2f;
    public float avoidAngleTau = 0.8f;

    [HideInInspector]
    public List<Boid> boids = new List<Boid>();

    [Header("Acceleration Field")]
    public int resolution = 16;
    public bool liveUpdateWhenPlaying = false;
    public float liveUpdateMaxFps = 15;

    float _lastSpawn = 0.0f;
    float _lastFieldUpdate = 0.0f;

    RenderTexture _accelFieldRT;
    Texture2D _accelFieldLocal;

    public Boid Spawn() {
        _lastSpawn = Time.time;
        Boid boid = Instantiate(prefab, this.transform);
        boid.flock = this;

        // Randomize position and orientation
        Vector3 deltaPos = spawnRadius * Random.insideUnitCircle.normalized;
        boid.transform.position = transform.position + deltaPos;
        boid.transform.up = -(Quaternion.AngleAxis(Random.Range(-90, +90), Vector3.back) * deltaPos.normalized);
        boid.speed = minSpeed + Random.value * (maxSpeed - minSpeed);

        boids.Add(boid);
        return boid;
    }

    public bool ShouldKill(Boid boid) {
        float radius = Vector3.Distance(boid.transform.position, transform.position);
        return (radius < killRadius) == (killRadius < spawnRadius);
    }


    private void KillInstance(Boid boid) {
        boid.flock = null;
        boid.transform.parent = null;
        boid.name = "[dead] " + boid.name;
        Destroy(boid);
    }

    public void Kill(Boid boid) {
        if (boids.Remove(boid)) {
            KillInstance(boid);
        }
    }

    public uint KillStrayBoids() {
        uint killed = 0;
        for (int i = boids.Count - 1; i >= 0; --i) {
            Boid boid = boids[i];
            if (ShouldKill(boid)) {
                ++killed;
                boids.RemoveAt(i);
                KillInstance(boid);
            }
        }
        return killed;
    }

    void Update() {
        // Spawn a boid if enough time has passed
        float spawnPeriod = 1.0f / spawnFrequency;
        float fieldRefreshPeriod = (liveUpdateMaxFps > 0.0f) ? 1.0f / liveUpdateMaxFps : 0.0f;

        if (Time.time - _lastSpawn > spawnPeriod && boids.Count < maxCount) {
            Spawn();
        }
        if (liveUpdateWhenPlaying && Time.time - _lastFieldUpdate > fieldRefreshPeriod) {
            UpdateAccelerationField();
        }
    }

    public Rect accelerationFieldDomain {
        get {
            float radius = Mathf.Max(spawnRadius, killRadius);
            return new Rect(
                transform.position.x - radius, transform.position.y - radius,
                2 * radius, 2 * radius
            );
        }
    }

    public Texture2D accelerationField {
        get {
            if (_accelFieldRT != null) {
                return _accelFieldLocal;
            }
            return null;
        }
    }

    public void UpdateAccelerationField() {
        _lastFieldUpdate = Time.time;

        BoidManager ownManager = null;
        var managers = Object.FindObjectsOfType(typeof(BoidManager));
        foreach (BoidManager manager in managers) {
            if (manager.flocks.Contains(this)) {
                ownManager = manager;
                break;
            }
        }

        if (ownManager == null) {
            Debug.LogWarning("Could not find manager for flock.");
            return;
        }

        if (_accelFieldRT == null || _accelFieldRT.width != resolution) {
            ClearAccelerationField();
            _accelFieldRT = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear);
            _accelFieldRT.enableRandomWrite = true;
            _accelFieldRT.Create();
        }


        ownManager.RenderFlockField(this, accelerationFieldDomain, _accelFieldRT);

        if (_accelFieldLocal == null || _accelFieldLocal.width != resolution)
        {
            _accelFieldLocal = new Texture2D(_accelFieldRT.width, _accelFieldRT.height, TextureFormat.RGFloat, false);
        }

        // Copy to the cache because we cannot directly access a rendertexture content:
        // RenderTextures do not exist on the CPU side.

        // When rendering gizmos, we're actually rendering to a texture. Gotta save that
        var oldRenderTexture = RenderTexture.active;
        // Lifesaver: https://discussions.unity.com/t/convert-a-rendertexture-to-a-texture2d/946/2
        RenderTexture.active = _accelFieldRT;
        _accelFieldLocal.ReadPixels(new Rect(0, 0, _accelFieldRT.width, _accelFieldRT.height), 0, 0);
        _accelFieldLocal.Apply();
        RenderTexture.active = oldRenderTexture;
    }

    public void ClearAccelerationField() {
        if (_accelFieldRT != null) {
            _accelFieldRT.Release();
            _accelFieldRT = null;
        }
    }

    void OnDestroy() {
        ClearAccelerationField();
    }
}


#if UNITY_EDITOR
[CustomEditor(typeof(Flock))]
public class FlockEditor : Editor {

    static void DrawVectorField(Rect rect, Texture2D field, float baseScaleFactor) {
        Vector2Int size = new Vector2Int(field.width, field.height);
        Vector2 pixelSize  = new Vector2(rect.width / size.x, rect.height / size.y);

        float cellSize = Mathf.Min(pixelSize.x, pixelSize.y);

        var data = field.GetRawTextureData<Vector2>();
        int i = 0;
        for (int y = 0; y < size.y; ++y) {
            for (int x = 0; x < size.x; ++x) {
                Vector2 pos = rect.min + 0.5f * pixelSize + pixelSize * new Vector2(x, y);
                Vector2 v = data[i++] * baseScaleFactor;

                // Clamp length at cell size, but make thicker from there onwards
                float length = v.magnitude;
                float scale = Mathf.Max(0.0f, length / cellSize);
                
                if (length > cellSize) {
                    v = v.normalized * cellSize;
                }

                Handles.DrawSolidDisc(pos, Vector3.back, 0.05f * cellSize * scale);
                Handles.DrawLine(pos, pos + v, scale);
            }
        }
    }

    [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.NotInSelectionHierarchy)]
    static void DrawGizmo(Flock flock, GizmoType gizmoType) {
        bool active = (gizmoType & GizmoType.Active) != 0;

        if (flock.accelerationField != null) {
            DrawVectorField(flock.accelerationFieldDomain, flock.accelerationField, 1.0f / flock.maxAcceleration);
        } else {
            for (uint i = 0; i < 36; ++i) {
                Vector3 dir = Quaternion.AngleAxis(i * 10.0f, Vector3.back) * Vector3.up;
                Handles.DrawLine(flock.transform.position + flock.spawnRadius * dir,
                                 flock.transform.position + flock.killRadius * dir);
            }
        }

        using (new Handles.DrawingScope(active ? Color.green : Handles.color)) {
            Handles.DrawWireDisc(flock.transform.position, Vector3.back, flock.spawnRadius);
        }
        using (new Handles.DrawingScope(active ? Color.red : Handles.color)) {
            Handles.DrawWireDisc(flock.transform.position, Vector3.back, flock.killRadius);
        }
    }

    void HandleRadius(Flock flock, ref float radius, string description) {
        EditorGUI.BeginChangeCheck();
        float newRadius = Handles.RadiusHandle(Quaternion.identity, flock.transform.position, radius, true);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(flock, description);
            radius = newRadius;
        }
    }

    public void OnSceneGUI() {
        Flock flock = target as Flock;

        using (new Handles.DrawingScope(Color.green)) {
            HandleRadius(flock, ref flock.spawnRadius, "Change spawn radius");
        }

        using (new Handles.DrawingScope(Color.red)) {
            HandleRadius(flock, ref flock.killRadius, "Change kill radius");
        }
    }

    public override void OnInspectorGUI() {
        Flock flock = target as Flock;

        DrawDefaultInspector();

        if (GUILayout.Button("Refresh field")) {
            flock.UpdateAccelerationField();
            UnityEditor.SceneView.RepaintAll();
        }

        if (GUILayout.Button("Clear field")) {
            flock.ClearAccelerationField();
            UnityEditor.SceneView.RepaintAll();
        }
    }
}
#endif