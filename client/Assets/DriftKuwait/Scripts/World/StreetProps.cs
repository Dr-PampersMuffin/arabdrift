using UnityEngine;

namespace DriftKuwait
{
    /// Recognition details from the blueprint (section 4): blue block signs (قطعة) with Arabic lettering on the avenue.
    /// Sign art is rendered with the game's fonts (tools: FX/sign_*.png), so the Arabic is always correct.
    public static class StreetProps
    {
        public static void Build(Transform root)
        {
            var parent = new GameObject("StreetProps").transform;
            parent.SetParent(root, false);
            var signTex = Resources.Load<Texture2D>("DriftKuwait/FX/sign_salwa");
            if (signTex == null) return;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var signMat = new Material(lit) { name = "M_Sign" };
            signMat.SetTexture("_BaseMap", signTex);
            signMat.SetFloat("_Smoothness", 0.55f);
            signMat.SetFloat("_AlphaClip", 1f);
            signMat.SetFloat("_Cutoff", 0.5f);
            signMat.EnableKeyword("_ALPHATEST_ON");
            var pole = Mat.Paint("M_SignPole", new Color(0.6f, 0.6f, 0.58f), 0.5f, 0.7f);

            foreach (float z in new[] { -116f, -52f, 76f, 140f })
                foreach (float side in new[] { 1f, -1f })
                {
                    float x = side * 12.8f;
                    var post = new MeshKit();
                    post.Cylinder(new Vector3(x - 0.9f, 1.6f, z), Vector3.up, 0.06f, 3.2f, 8);
                    post.Cylinder(new Vector3(x + 0.9f, 1.6f, z), Vector3.up, 0.06f, 3.2f, 8);
                    post.ToObject("SignPost", parent, pole);
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.Destroy(q.GetComponent<Collider>());
                    q.name = "BlockSign";
                    q.transform.SetParent(parent, false);
                    q.transform.position = new Vector3(x, 3.3f, z + (side > 0 ? -0.05f : 0.05f));
                    // Right-hand signs face oncoming (+Z) traffic, left-hand ones face the other way.
                    q.transform.rotation = side > 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                    q.transform.localScale = new Vector3(2.4f, 2.0f, 1f);
                    q.GetComponent<MeshRenderer>().sharedMaterial = signMat;
                    var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.Destroy(back.GetComponent<Collider>());
                    back.transform.SetParent(q.transform, false);
                    back.transform.localPosition = new Vector3(0f, 0f, 0.01f);
                    back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    back.GetComponent<MeshRenderer>().sharedMaterial = pole;
                }
        }
    }
}
