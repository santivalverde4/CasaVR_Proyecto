// ARCHIVO GENERADO por tools/gen_mattable.py a partir de la tabla de materiales del modelo.
using UnityEngine;

namespace CasaVR.EditorTools
{
    public struct MatDef
    {
        public string name;
        public string texture;
        public Color color;
        public float smoothness;
        public float metallic;
        public float alpha;
        public Color emission;
        public float textureMeters;

        public MatDef(string name, string texture, Color color, float smoothness, float metallic, float alpha, Color emission, float textureMeters)
        {
            this.name = name; this.texture = texture; this.color = color; this.smoothness = smoothness;
            this.metallic = metallic; this.alpha = alpha; this.emission = emission; this.textureMeters = textureMeters;
        }
    }

    public static class CasaVRMaterialTable
    {
        public static readonly MatDef[] All =
        {
            new MatDef("M_Pared_Interior", "T_Estuco", new Color(0.930f, 0.900f, 0.850f), 0.10f, 0.00f, 1.00f, Color.black, 2.000f),
            new MatDef("M_Pared_Acento", "T_Estuco", new Color(0.640f, 0.690f, 0.600f), 0.10f, 0.00f, 1.00f, Color.black, 2.000f),
            new MatDef("M_Pared_Escalera", "T_Estuco", new Color(0.860f, 0.850f, 0.820f), 0.10f, 0.00f, 1.00f, Color.black, 2.000f),
            new MatDef("M_Fachada", "T_Estuco", new Color(0.820f, 0.770f, 0.680f), 0.05f, 0.00f, 1.00f, Color.black, 2.000f),
            new MatDef("M_Azulejo_Bano", "T_Azulejo", new Color(1.000f, 1.000f, 1.000f), 0.60f, 0.00f, 1.00f, Color.black, 0.600f),
            new MatDef("M_Azulejo_Lavanderia", "T_Azulejo_Cocina", new Color(1.000f, 1.000f, 1.000f), 0.55f, 0.00f, 1.00f, Color.black, 0.600f),
            new MatDef("M_Azulejo_Cocina", "T_Azulejo_Cocina", new Color(1.000f, 1.000f, 1.000f), 0.60f, 0.00f, 1.00f, Color.black, 0.600f),
            new MatDef("M_Techo", "T_Estuco", new Color(0.970f, 0.960f, 0.940f), 0.05f, 0.00f, 1.00f, Color.black, 2.000f),
            new MatDef("M_Cubierta", "T_Cubierta", new Color(1.000f, 1.000f, 1.000f), 0.45f, 0.30f, 1.00f, Color.black, 2.000f),
            new MatDef("M_Fascia", null, new Color(0.920f, 0.920f, 0.900f), 0.30f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Concreto", "T_Concreto", new Color(1.000f, 1.000f, 1.000f), 0.10f, 0.00f, 1.00f, Color.black, 2.000f),
            new MatDef("M_Piso_Madera", "T_Madera_Piso", new Color(1.000f, 1.000f, 1.000f), 0.45f, 0.00f, 1.00f, Color.black, 1.080f),
            new MatDef("M_Piso_Porcelanato", "T_Porcelanato", new Color(1.000f, 1.000f, 1.000f), 0.65f, 0.00f, 1.00f, Color.black, 1.200f),
            new MatDef("M_Piso_Ceramica", "T_Ceramica", new Color(1.000f, 1.000f, 1.000f), 0.50f, 0.00f, 1.00f, Color.black, 1.200f),
            new MatDef("M_Piso_Exterior", "T_Pavimento", new Color(1.000f, 1.000f, 1.000f), 0.15f, 0.00f, 1.00f, Color.black, 1.600f),
            new MatDef("M_Piso_Deck", "T_Deck", new Color(1.000f, 1.000f, 1.000f), 0.30f, 0.00f, 1.00f, Color.black, 1.120f),
            new MatDef("M_Umbral", "T_Marmol", new Color(1.000f, 1.000f, 1.000f), 0.70f, 0.00f, 1.00f, Color.black, 0.600f),
            new MatDef("M_Escalera_Huella", "T_Concreto", new Color(0.880f, 0.860f, 0.820f), 0.20f, 0.00f, 1.00f, Color.black, 2.000f),
            new MatDef("M_Madera_Puerta", "T_Madera_Mueble", new Color(0.820f, 0.640f, 0.470f), 0.40f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Madera_Puerta_Principal", "T_Madera_Mueble", new Color(0.500f, 0.340f, 0.220f), 0.45f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Marco_Puerta", null, new Color(0.930f, 0.920f, 0.900f), 0.40f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Metal_Cromo", null, new Color(0.860f, 0.860f, 0.870f), 0.85f, 1.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Aluminio", null, new Color(0.160f, 0.160f, 0.170f), 0.50f, 0.60f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Vidrio", null, new Color(0.800f, 0.900f, 0.950f), 0.95f, 0.00f, 0.18f, Color.black, 1.000f),
            new MatDef("M_Moldura", null, new Color(0.940f, 0.930f, 0.910f), 0.40f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Madera_Celosia", "T_Madera_Mueble", new Color(0.560f, 0.390f, 0.250f), 0.30f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Columna", null, new Color(0.200f, 0.200f, 0.210f), 0.40f, 0.50f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Tela_Sofa", "T_Tela", new Color(0.380f, 0.440f, 0.490f), 0.05f, 0.00f, 1.00f, Color.black, 0.500f),
            new MatDef("M_Cojin_Acento", "T_Tela", new Color(0.860f, 0.620f, 0.220f), 0.05f, 0.00f, 1.00f, Color.black, 0.500f),
            new MatDef("M_Cojin_Claro", "T_Tela", new Color(0.900f, 0.870f, 0.800f), 0.05f, 0.00f, 1.00f, Color.black, 0.500f),
            new MatDef("M_Madera_Mueble", "T_Madera_Mueble", new Color(1.000f, 1.000f, 1.000f), 0.35f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Madera_Oscura", "T_Madera_Mueble", new Color(0.420f, 0.280f, 0.180f), 0.35f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Laca_Blanca", null, new Color(0.930f, 0.920f, 0.900f), 0.60f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Gabinete_Cocina", null, new Color(0.360f, 0.440f, 0.400f), 0.50f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Encimera", "T_Marmol", new Color(1.000f, 1.000f, 1.000f), 0.75f, 0.00f, 1.00f, Color.black, 0.600f),
            new MatDef("M_Acero_Inox", null, new Color(0.760f, 0.770f, 0.780f), 0.75f, 1.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Porcelana", null, new Color(0.960f, 0.960f, 0.950f), 0.85f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Negro_Mate", null, new Color(0.060f, 0.060f, 0.060f), 0.20f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Pantalla_TV", null, new Color(0.020f, 0.020f, 0.025f), 0.90f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Espejo", null, new Color(0.900f, 0.920f, 0.940f), 0.98f, 1.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Plastico_Blanco", null, new Color(0.930f, 0.930f, 0.930f), 0.50f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Tela_Cama", "T_Tela", new Color(0.960f, 0.960f, 0.940f), 0.05f, 0.00f, 1.00f, Color.black, 0.500f),
            new MatDef("M_Tela_Cobija", "T_Tela", new Color(0.450f, 0.560f, 0.660f), 0.05f, 0.00f, 1.00f, Color.black, 0.500f),
            new MatDef("M_Tela_Cabecera", "T_Tela", new Color(0.580f, 0.530f, 0.480f), 0.05f, 0.00f, 1.00f, Color.black, 0.500f),
            new MatDef("M_Alfombra", "T_Tela", new Color(0.740f, 0.680f, 0.600f), 0.02f, 0.00f, 1.00f, Color.black, 0.500f),
            new MatDef("M_Maceta", null, new Color(0.660f, 0.380f, 0.250f), 0.20f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Tierra", "T_Concreto", new Color(0.360f, 0.260f, 0.170f), 0.00f, 0.00f, 1.00f, Color.black, 2.000f),
            new MatDef("M_Hoja", "T_Hojas", new Color(0.950f, 1.000f, 0.900f), 0.25f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Hoja_Clara", null, new Color(0.380f, 0.580f, 0.240f), 0.25f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Corteza", "T_Madera_Mueble", new Color(0.450f, 0.340f, 0.250f), 0.10f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Cesped", "T_Cesped", new Color(1.000f, 1.000f, 1.000f), 0.10f, 0.00f, 1.00f, Color.black, 4.000f),
            new MatDef("M_Seto", "T_Hojas", new Color(0.750f, 0.860f, 0.700f), 0.20f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Flor_Roja", null, new Color(0.850f, 0.150f, 0.200f), 0.30f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Flor_Amarilla", null, new Color(0.960f, 0.800f, 0.200f), 0.30f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Lampara_Base", null, new Color(0.950f, 0.950f, 0.940f), 0.50f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Lampara_Difusor", null, new Color(1.000f, 0.970f, 0.900f), 0.50f, 0.00f, 1.00f, new Color(1.000f, 0.880f, 0.700f), 1.000f),
            new MatDef("M_Interruptor", null, new Color(0.960f, 0.960f, 0.950f), 0.50f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Pila", "T_Concreto", new Color(0.860f, 0.860f, 0.840f), 0.30f, 0.00f, 1.00f, Color.black, 2.000f),
            new MatDef("M_Vidrio_Oscuro", null, new Color(0.100f, 0.120f, 0.140f), 0.95f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Vitroceramica", null, new Color(0.020f, 0.020f, 0.020f), 0.95f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Libro_A", null, new Color(0.580f, 0.220f, 0.200f), 0.30f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Libro_B", null, new Color(0.220f, 0.340f, 0.520f), 0.30f, 0.00f, 1.00f, Color.black, 1.000f),
            new MatDef("M_Ratan", "T_Tela", new Color(0.740f, 0.600f, 0.420f), 0.10f, 0.00f, 1.00f, Color.black, 0.500f),
            new MatDef("M_Toalla", "T_Tela", new Color(0.820f, 0.900f, 0.900f), 0.02f, 0.00f, 1.00f, Color.black, 0.500f),
        };
    }
}
