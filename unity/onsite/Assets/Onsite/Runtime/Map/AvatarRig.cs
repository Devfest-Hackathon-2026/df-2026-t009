using UnityEngine;

namespace Onsite.Map
{
    public enum BodyType { Gentleman, Lady, Gown, Goblin }
    public enum AvatarPose { Stand, Wave, Pose }
    public enum Finish { Gold, Silver, RoseGold, Crystal }

    [System.Serializable]
    public struct AvatarLook
    {
        public BodyType body;
        public AvatarPose pose;
        public Finish finish;

        public static AvatarLook Default => new() { body = BodyType.Gentleman, pose = AvatarPose.Stand, finish = Finish.Gold };

        public string ModelName => $"avatar_{body.ToString().ToLowerInvariant()}_{pose.ToString().ToLowerInvariant()}";

        public static AvatarLook Load()
        {
            var l = Default;
            l.body = (BodyType)PlayerPrefs.GetInt("onsite.look.body", 0);
            l.pose = (AvatarPose)PlayerPrefs.GetInt("onsite.look.pose", 0);
            l.finish = (Finish)PlayerPrefs.GetInt("onsite.look.finish", 0);
            return l;
        }

        public void Save()
        {
            PlayerPrefs.SetInt("onsite.look.body", (int)body);
            PlayerPrefs.SetInt("onsite.look.pose", (int)pose);
            PlayerPrefs.SetInt("onsite.look.finish", (int)finish);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// トロフィー像のアバター。Resources/Avatars/avatar_&lt;体&gt;_&lt;ポーズ&gt; を読み、
    /// 材質名（Body / Crystal / Base）で仕上げを差し替える。地図の上では大きく立てる。
    /// </summary>
    public class AvatarRig : MonoBehaviour
    {
        public float scale = 18f;
        public bool spin = false;
        GameObject _model;
        AvatarLook _look;
        LineRenderer _halo;
        Material _haloMat;

        public void Apply(AvatarLook look)
        {
            _look = look;
            if (_model != null) Destroy(_model);
            var prefab = Resources.Load<GameObject>($"Avatars/{look.ModelName}") ?? Resources.Load<GameObject>("Avatars/avatar_gentleman_stand");
            if (prefab == null)
            {
                // モデルがまだ無いときは、光る柱で代わりにする
                _model = Shapes.Beam(transform, Shapes.GlowMaterial(MarkerField.Amber * 3f, fadeY: 0.6f), 1f, 0.25f, 8);
            }
            else
            {
                _model = Instantiate(prefab, transform);
                Tint(_model, look.finish);
            }
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localScale = Vector3.one * scale;
            if (_halo == null)
            {
                _haloMat = Shapes.GlowMaterial(MarkerField.Cyan * 1.6f, pulse: 2.2f);
                _halo = Shapes.Line(transform, "Halo", _haloMat, 1.4f);
            }
        }

        void LateUpdate()
        {
            if (_halo != null) Shapes.Ring(_halo, transform.position + Vector3.up * 0.4f, scale * 0.55f, 64);
            if (spin && _model != null) _model.transform.Rotate(0, 25f * Time.deltaTime, 0, Space.Self);
        }

        public static void Tint(GameObject root, Finish finish)
        {
            (Color body, float metal, float rough, Color crystal) = finish switch
            {
                Finish.Silver => (new Color(0.86f, 0.88f, 0.90f), 1f, 0.16f, new Color(0.70f, 0.92f, 1f)),
                Finish.RoseGold => (new Color(0.89f, 0.62f, 0.55f), 1f, 0.2f, new Color(1f, 0.78f, 0.80f)),
                Finish.Crystal => (new Color(0.70f, 0.92f, 1f), 0.15f, 0.04f, new Color(0.85f, 0.97f, 1f)),
                _ => (new Color(0.85f, 0.64f, 0.26f), 1f, 0.18f, new Color(1f, 0.81f, 0.42f)),
            };
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.materials; // 実体を複製して色を変える
                foreach (var m in mats)
                {
                    string n = m.name.ToLowerInvariant();
                    if (n.StartsWith("body"))
                    {
                        SetColor(m, body); SetFloat(m, "_Metallic", metal); SetFloat(m, "_Smoothness", 1 - rough);
                        SetFloat(m, "metallicFactor", metal); SetFloat(m, "roughnessFactor", rough);
                    }
                    else if (n.StartsWith("crystal"))
                    {
                        SetColor(m, crystal);
                        if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", crystal * 0.9f); }
                        if (m.HasProperty("emissiveFactor")) m.SetColor("emissiveFactor", crystal * 0.9f);
                    }
                    // 測量マップの効果のあとに描かれるよう、透明キューへ
                    m.renderQueue = 3000;
                }
                r.materials = mats;
            }
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("baseColorFactor")) m.SetColor("baseColorFactor", c);
        }

        static void SetFloat(Material m, string k, float v)
        {
            if (m.HasProperty(k)) m.SetFloat(k, v);
        }
    }
}
