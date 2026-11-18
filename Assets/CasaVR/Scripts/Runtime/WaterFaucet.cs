using UnityEngine;

namespace CasaVR
{
    /// <summary>
    /// Llave / tubo con agua: al abrir, la manija gira y el agua corre (chorro con textura animada,
    /// gotas con gravedad y salpicaduras). Tipo Ducha: rocío en cono desde la regadera.
    /// Los efectos se generan por código a partir de los marcadores del modelo (salida y fondo).
    /// </summary>
    public class WaterFaucet : CasaInteractable
    {
        public enum Kind { Grifo, Ducha }

        public Kind kind = Kind.Grifo;
        public Transform handle;
        [Tooltip("Eje de giro de la manija en el espacio local de la manija.")]
        public Vector3 handleAxisLocal = Vector3.right;
        public float openAngle = 60f;
        public float handleSpeed = 240f;
        public Transform outlet;
        public Transform bottom;
        public Material streamMaterial;
        public Material dropMaterial;
        public AudioClip waterLoop;
        public AudioClip clickClip;

        bool m_Open;
        float m_HandleAngle;
        Quaternion m_HandleBase;
        ParticleSystem m_Drops;
        ParticleSystem m_Splash;
        GameObject m_Stream;
        Material m_StreamInstance;
        AudioSource m_Loop;
        bool m_Running;

        public bool IsOpen => m_Open;
        public bool IsWaterRunning => m_Running;
        public override string Prompt => m_Open ? "Cerrar la llave" : "Abrir la llave";

        protected override void Awake()
        {
            base.Awake();
            if (handle != null) m_HandleBase = handle.localRotation;
            BuildEffects();
        }

        public override void Interact()
        {
            m_Open = !m_Open;
            PlayClip(clickClip, 0.6f);
        }

        public void SetOpen(bool open)
        {
            if (open != m_Open) Interact();
        }

        protected override void Update()
        {
            base.Update();
            float target = m_Open ? openAngle : 0f;
            if (!Mathf.Approximately(m_HandleAngle, target))
            {
                m_HandleAngle = Mathf.MoveTowards(m_HandleAngle, target, handleSpeed * Time.deltaTime);
                if (handle != null) handle.localRotation = m_HandleBase * Quaternion.AngleAxis(m_HandleAngle, handleAxisLocal);
            }
            bool shouldRun = Mathf.Abs(m_HandleAngle) > Mathf.Abs(openAngle) * 0.35f && m_Open;
            if (shouldRun != m_Running) SetRunning(shouldRun);
            if (m_Running && m_StreamInstance != null)
            {
                Vector2 off = m_StreamInstance.mainTextureOffset;
                off.y -= Time.deltaTime * 2.2f;
                if (off.y < -100f) off.y += 100f;
                m_StreamInstance.mainTextureOffset = off;
                if (m_StreamInstance.HasProperty("_BaseMap")) m_StreamInstance.SetTextureOffset("_BaseMap", off);
            }
        }

        void SetRunning(bool run)
        {
            m_Running = run;
            if (m_Stream != null) m_Stream.SetActive(run);
            if (run)
            {
                if (m_Drops != null) m_Drops.Play(true);
                if (m_Splash != null) m_Splash.Play(true);
                if (m_Loop != null && m_Loop.clip != null) m_Loop.Play();
            }
            else
            {
                if (m_Drops != null) m_Drops.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (m_Splash != null) m_Splash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (m_Loop != null) m_Loop.Stop();
            }
        }

        // ------------------------------------------------------------------ efectos
        void BuildEffects()
        {
            if (outlet == null) return;
            float fall = bottom != null ? Mathf.Max(0.05f, outlet.position.y - bottom.position.y) : 0.3f;
            Vector3 floorPoint = bottom != null ? new Vector3(outlet.position.x, bottom.position.y, outlet.position.z)
                                                : outlet.position + Vector3.down * fall;

            if (kind == Kind.Grifo)
            {
                m_Stream = BuildStream(fall);
                m_Drops = CreateSystem("Gotas", outlet.position, Quaternion.LookRotation(Vector3.down, Vector3.forward));
                ConfigureDrops(m_Drops, fall, 0.35f, 0.004f, 3f, 60f, 0.006f, 0.011f);
            }
            else
            {
                m_Drops = CreateSystem("Rocio", outlet.position, Quaternion.LookRotation(Vector3.down, Vector3.forward));
                ConfigureDrops(m_Drops, fall, 1.6f, 0.07f, 11f, 700f, 0.006f, 0.012f);
            }
            m_Splash = CreateSystem("Salpicadura", floorPoint + Vector3.up * 0.005f, Quaternion.LookRotation(Vector3.up, Vector3.forward));
            ConfigureSplash(m_Splash, kind == Kind.Ducha ? 0.22f : 0.015f, kind == Kind.Ducha ? 160f : 45f);

            m_Loop = gameObject.AddComponent<AudioSource>();
            m_Loop.clip = waterLoop;
            m_Loop.loop = true;
            m_Loop.playOnAwake = false;
            m_Loop.spatialBlend = 1f;
            m_Loop.minDistance = 0.4f;
            m_Loop.maxDistance = 9f;
            m_Loop.rolloffMode = AudioRolloffMode.Linear;
            m_Loop.volume = kind == Kind.Ducha ? 0.8f : 0.55f;
            SetRunning(false);
        }

        ParticleSystem CreateSystem(string name, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, true);
            go.transform.SetPositionAndRotation(pos, rot);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = dropMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static float FallTime(float h, float v0)
        {
            const float g = 9.81f;
            return (-v0 + Mathf.Sqrt(v0 * v0 + 2f * g * h)) / g;
        }

        static void ConfigureDrops(ParticleSystem ps, float fall, float speed, float radius, float angle, float rate,
                                   float sizeMin, float sizeMax)
        {
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.85f, speed * 1.15f);
            main.startLifetime = FallTime(fall, speed) * 0.98f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.gravityModifier = 1f;
            main.startColor = new Color(0.85f, 0.93f, 1f, 0.55f);
            var em = ps.emission;
            em.rateOverTime = rate;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = angle;
            sh.radius = radius;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f;
            r.lengthScale = 1.2f;
        }

        static void ConfigureSplash(ParticleSystem ps, float radius, float rate)
        {
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.01f);
            main.gravityModifier = 1f;
            main.startColor = new Color(0.9f, 0.95f, 1f, 0.6f);
            var em = ps.emission;
            em.rateOverTime = rate;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 55f;
            sh.radius = radius;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
        }

        GameObject BuildStream(float length)
        {
            var go = new GameObject("Chorro");
            go.transform.SetParent(transform, true);
            go.transform.SetPositionAndRotation(outlet.position, Quaternion.identity);
            const int seg = 8;
            const float r0 = 0.0065f, r1 = 0.0045f;
            var v = new Vector3[(seg + 1) * 2];
            var uv = new Vector2[v.Length];
            var t = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i] = d * r0;
                v[i + seg + 1] = d * r1 + Vector3.down * length;
                uv[i] = new Vector2(i / (float)seg, 0f);
                uv[i + seg + 1] = new Vector2(i / (float)seg, length * 4f);
            }
            for (int i = 0; i < seg; i++)
            {
                int k = i * 6;
                t[k] = i; t[k + 1] = i + 1; t[k + 2] = i + seg + 1;
                t[k + 3] = i + 1; t[k + 4] = i + seg + 2; t[k + 5] = i + seg + 1;
            }
            var mesh = new Mesh { name = "Chorro" };
            mesh.vertices = v; mesh.uv = uv; mesh.triangles = t;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (streamMaterial != null)
            {
                m_StreamInstance = new Material(streamMaterial);
                mr.sharedMaterial = m_StreamInstance;
            }
            go.SetActive(false);
            return go;
        }
    }
}
