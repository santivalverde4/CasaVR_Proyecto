using UnityEngine;
using UnityEngine.Rendering;

namespace CasaVR
{
    /// <summary>Cambia entre día y noche (sol, luz ambiente y cielo). De noche se nota el efecto de los interruptores.</summary>
    public class DayNightController : MonoBehaviour
    {
        public Light sun;
        public Material skybox;
        public bool night;

        [Header("Día")]
        public float daySunIntensity = 1.6f;
        public Color dayAmbientSky = new Color(0.62f, 0.68f, 0.78f);
        public Color dayAmbientEquator = new Color(0.52f, 0.52f, 0.50f);
        public Color dayAmbientGround = new Color(0.30f, 0.28f, 0.25f);
        public float daySkyExposure = 1.2f;

        [Header("Noche")]
        public float nightSunIntensity = 0.06f;
        public Color nightAmbientSky = new Color(0.05f, 0.06f, 0.10f);
        public Color nightAmbientEquator = new Color(0.035f, 0.035f, 0.05f);
        public Color nightAmbientGround = new Color(0.02f, 0.02f, 0.025f);
        public float nightSkyExposure = 0.08f;

        public event System.Action<bool> Changed;

        void Start() => Apply();

        public void Toggle()
        {
            night = !night;
            Apply();
        }

        public void Apply()
        {
            if (sun != null) sun.intensity = night ? nightSunIntensity : daySunIntensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = night ? nightAmbientSky : dayAmbientSky;
            RenderSettings.ambientEquatorColor = night ? nightAmbientEquator : dayAmbientEquator;
            RenderSettings.ambientGroundColor = night ? nightAmbientGround : dayAmbientGround;
            if (skybox != null && skybox.HasProperty("_Exposure"))
                skybox.SetFloat("_Exposure", night ? nightSkyExposure : daySkyExposure);
            Changed?.Invoke(night);
        }

        void OnDestroy()
        {
            // deja el material del cielo como estaba (es un asset)
            if (skybox != null && skybox.HasProperty("_Exposure")) skybox.SetFloat("_Exposure", daySkyExposure);
        }
    }
}
