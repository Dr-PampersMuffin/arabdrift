using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DriftKuwait
{
    /// التعديل: the customs shop over the garage (the car stays on the turntable on the left so every choice can be
    /// tried on before buying). Three views: the 12 categories with big pictures, a category's items as big cards,
    /// and the pearl packs with the starter offer and "restore purchases".
    public class ShopScreen : MonoBehaviour
    {
        enum View { Categories, Items, Packs }

        GarageScreen garage;
        GameFlow flow;
        RectTransform safe, panel, body;
        Text pearlsTxt, cashTxt;
        string car;
        View view;
        Customs.Cat cat;
        readonly List<GameObject> hidden = new List<GameObject>();

        public static ShopScreen Current { get; private set; }

        static readonly Customs.Cat[] Grid =
        {
            Customs.Cat.Engine, Customs.Cat.Turbo, Customs.Cat.Nitro, Customs.Cat.Rims,
            Customs.Cat.Paint, Customs.Cat.Stickers, Customs.Cat.Slogans, Customs.Cat.Fenders,
            Customs.Cat.Wing, Customs.Cat.Exhaust, Customs.Cat.Smoke, Customs.Cat.Neon,
        };

        static readonly Color PearlCol = new Color(0.93f, 0.96f, 1f), Panel = new Color(0.035f, 0.06f, 0.11f, 0.95f);
        const float W = 1180f, H = 940f;

        public static ShopScreen Open(GarageScreen g, GameFlow flow, RectTransform safe, string carId)
        {
            if (Current != null) Destroy(Current.gameObject);
            var root = UIKit.Rect(safe, "Shop");
            UIKit.Stretch(root);
            var s = root.gameObject.AddComponent<ShopScreen>();
            s.garage = g; s.flow = flow; s.safe = safe; s.car = carId;
            Current = s;
            s.Build();
            return s;
        }

        void Build()
        {
            // the shop takes the right side; the garage's own side panels and carousel step away while it is open
            foreach (var n in new[] { "Stats", "Tiles", "Actions", "PrevCar", "NextCar", "TopRight", "Back", "Workshop" })
            {
                var t = safe.Find(n);
                if (t != null && t.gameObject.activeSelf) { t.gameObject.SetActive(false); hidden.Add(t.gameObject); }
            }
            var p = UIKit.Panel(transform, "Panel", Panel, "hud/panel_sharp");
            UIKit.Place(p.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -112f), new Vector2(W, H));
            UIKit.Outline(p.transform, new Color(1f, 1f, 1f, 0.16f), "hud/panel_sharp_outline");
            panel = p.rectTransform;

            // header: title (right), balances, back (left)
            var title = UIKit.Label(panel, "Title", "التعديل", 64, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Display);
            UIKit.Place(title.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -6f), new Vector2(330f, 92f));
            var back = UIKit.Button(panel, "Back", UIKit.Ink, Back, "hud/pill");
            UIKit.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(170f, 74f));
            UIKit.Outline(back.transform, new Color(1, 1, 1, 0.25f), "hud/pill");
            var bt = UIKit.Label(back.transform, "T", "رجوع", 38, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(bt.rectTransform);
            cashTxt = Chip(new Vector2(210f, -16f), UIKit.Gold, null);
            pearlsTxt = Chip(new Vector2(470f, -16f), PearlCol, () => Show(View.Packs));
            Customs.Changed += Refresh;
            Show(View.Categories);
        }

        Text Chip(Vector2 pos, Color coin, System.Action plus)
        {
            var c = UIKit.Panel(panel, "Chip", new Color(0.02f, 0.03f, 0.06f, 0.9f), "hud/pill");
            UIKit.Place(c.rectTransform, new Vector2(0f, 1f), pos, new Vector2(240f, 74f));
            var ic = UIKit.Img(c.transform, "Coin", UIKit.S("hud/circle_mask"), coin);
            UIKit.Place(ic.rectTransform, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(46f, 46f));
            var t = UIKit.Label(c.transform, "V", "", 34, Color.white, TextAnchor.MiddleCenter, UIKit.Digital, false);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(plus != null ? -4f : -18f, 0f), new Vector2(150f, 60f));
            UIKit.Fit(t, 22, 34);
            if (plus != null)
            {
                var b = UIKit.Button(c.transform, "Plus", UIKit.Gold, () => plus(), "hud/circle_mask");
                UIKit.Place((RectTransform)b.transform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(54f, 54f));
                var pl = UIKit.Label(b.transform, "T", "+", 44, new Color(0.12f, 0.08f, 0.03f), TextAnchor.MiddleCenter, UIKit.Black, false);
                UIKit.Stretch(pl.rectTransform);
            }
            return t;
        }

        void Refresh()
        {
            if (pearlsTxt != null) pearlsTxt.text = UIKit.Dig(Customs.Pearls);
            if (cashTxt != null) cashTxt.text = UIKit.Dig(Wallet.Cash);
        }

        void Back()
        {
            Sfx.Play("ui_click");
            if (view == View.Categories) Close();
            else Show(View.Categories);
        }

        public void Close()
        {
            Customs.ClearPreview();
            foreach (var g in hidden) if (g != null) g.SetActive(true);
            garage?.Refresh();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            Customs.Changed -= Refresh;
            if (Current == this) Current = null;
        }

        void Show(View v)
        {
            view = v;
            if (v != View.Items && (Customs.PreviewCar != null)) { Customs.ClearPreview(); garage?.Refresh(); }
            if (body != null) Destroy(body.gameObject);
            body = UIKit.Rect(panel, "Body");
            UIKit.Stretch(body, 20f, 20f, 20f, 112f);   // below the header
            Refresh();
            if (v == View.Categories) BuildCategories();
            else if (v == View.Items) BuildItems();
            else BuildPacks();
        }

        // ------------------------------------------------------------------ categories
        void BuildCategories()
        {
            float top = 0f;
            if (!Customs.StarterOwned) { StarterBanner(body, top); top += 118f; }
            float tw = (W - 40f - 36f) / 4f, th = (H - 132f - top - 56f - 24f) / 3f;
            for (int i = 0; i < Grid.Length; i++)
            {
                var c = Grid[i];
                int col = i % 4, row = i / 4;
                var tile = UIKit.Button(body, "Tile_" + c, new Color(0.02f, 0.04f, 0.08f, 1f), () => OpenCat(c), "hud/panel_sharp");
                var rt = (RectTransform)tile.transform;
                UIKit.Place(rt, new Vector2(1f, 1f), new Vector2(-col * (tw + 12f), -top - row * (th + 12f)), new Vector2(tw, th));
                var clip = UIKit.Rect(rt, "Clip"); UIKit.Stretch(clip, 3f, 3f, 3f, 3f); clip.gameObject.AddComponent<RectMask2D>();
                string tile_ = Customs.CatTile(c);
                if (tile_ != null)
                {
                    var tex = Resources.Load<Texture2D>("DriftKuwait/Customs/Tiles/" + tile_);
                    if (tex != null) { var ri = UIKit.Cover(clip, "Img", tex); ri.raycastTarget = false; }
                }
                else SloganTile(clip);
                var shade = UIKit.Img(clip, "Shade", UIKit.S("hud/grad_v"), new Color(0f, 0f, 0f, 0.85f));
                shade.raycastTarget = false;
                UIKit.Place(shade.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(tw, th * 0.62f));
                shade.rectTransform.localScale = new Vector3(1f, -1f, 1f);   // dark at the bottom, behind the name
                var nm = UIKit.Label(rt, "N", Customs.CatAr(c), 44, UIKit.Cream, TextAnchor.LowerRight, UIKit.Black);
                UIKit.Place(nm.rectTransform, new Vector2(1f, 0f), new Vector2(-14f, 40f), new Vector2(tw - 24f, 56f));
                UIKit.Fit(nm, 30, 44); UIKit.AddShadow(nm, 2f, 0.9f);
                var sb = UIKit.Label(rt, "S", Customs.CatSubAr(c), 30, new Color(1f, 1f, 1f, 0.85f), TextAnchor.LowerRight, UIKit.Arabic);
                UIKit.Place(sb.rectTransform, new Vector2(1f, 0f), new Vector2(-14f, 6f), new Vector2(tw - 24f, 40f));
                UIKit.Fit(sb, 24, 30);
                PricePill(rt, Customs.IsSpeed(c), Customs.IsSpeed(c) ? FromCash(c) : FromPearls(c), new Vector2(10f, -10f));
            }
            var legend = UIKit.Label(body, "Legend", "القطع اللي تزيد القوة بالكاش من التقحيص بس  ·  الشكل والستايل باللؤلؤ", 30,
                new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter, UIKit.Arabic);
            UIKit.Place(legend.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(W - 60f, 48f));
            UIKit.Fit(legend, 24, 32);
        }

        static long FromCash(Customs.Cat c) => c == Customs.Cat.Nitro ? Customs.NitroPrice[0] : Tuning.Price(c == Customs.Cat.Engine ? Tuning.Part.Engine : Tuning.Part.Turbo, 1);

        static long FromPearls(Customs.Cat c)
        {
            int min = int.MaxValue;
            if (c == Customs.Cat.Rims) { for (int i = Tuning.CashRims; i < Tuning.Rims.Length; i++) min = Mathf.Min(min, Tuning.Rims[i].pearls); }
            else if (c == Customs.Cat.Paint) { for (int i = Tuning.CashPaints; i < Tuning.Paints.Length; i++) min = Mathf.Min(min, Tuning.Paints[i].pearls); }
            else if (Customs.Items.TryGetValue(c, out var list)) foreach (var it in list) min = Mathf.Min(min, it.pearls);
            return min == int.MaxValue ? 0 : min;
        }

        void SloganTile(RectTransform parent)
        {
            var bg = UIKit.Img(parent, "Bg", null, new Color(0.04f, 0.08f, 0.16f, 1f));
            bg.raycastTarget = false; UIKit.Stretch(bg.rectTransform);
            string[] keys = { "slg_salwa", "slg_king" };
            for (int i = 0; i < 2; i++)
            {
                var band = UIKit.Img(parent, "Band" + i, UIKit.S("hud/pill"), i == 0 ? new Color(0.02f, 0.02f, 0.02f, 1f) : new Color(0.35f, 0.04f, 0.04f, 1f));
                band.raycastTarget = false;
                band.rectTransform.anchorMin = new Vector2(0.06f, 0.78f - i * 0.26f); band.rectTransform.anchorMax = new Vector2(0.94f, 0.96f - i * 0.26f);
                band.rectTransform.offsetMin = band.rectTransform.offsetMax = Vector2.zero;
                var tex = Resources.Load<Texture2D>("DriftKuwait/Customs/" + keys[i]);
                if (tex == null) continue;
                var ri = new GameObject("Txt", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                ri.transform.SetParent(band.transform, false); ri.texture = tex; ri.raycastTarget = false;
                UIKit.Stretch(ri.rectTransform, 8f, 8f, 4f, 4f);
            }
        }

        void PricePill(RectTransform parent, bool cash, long amount, Vector2 pos, bool from = true)
        {
            var pp = UIKit.Panel(parent, "Price", new Color(0.02f, 0.03f, 0.06f, 0.9f), "hud/pill");
            pp.raycastTarget = false;
            UIKit.Place(pp.rectTransform, new Vector2(0f, 1f), pos, new Vector2(170f, 50f));
            var ic = UIKit.Img(pp.transform, "C", UIKit.S("hud/circle_mask"), cash ? UIKit.Gold : PearlCol);
            ic.raycastTarget = false;
            UIKit.Place(ic.rectTransform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(32f, 32f));
            var t = UIKit.Label(pp.transform, "V", UIKit.Dig(amount), 28, Color.white, TextAnchor.MiddleCenter, UIKit.Digital, false);
            t.raycastTarget = false;
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-16f, 0f), new Vector2(120f, 44f));
            UIKit.Fit(t, 18, 28);
        }

        void StarterBanner(RectTransform parent, float top)
        {
            var b = UIKit.Button(parent, "Starter", new Color(0.72f, 0.2f, 0.06f, 1f), BuyStarter, "hud/panel_sharp");
            var rt = (RectTransform)b.transform;
            UIKit.Place(rt, new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(W - 40f, 106f));
            UIKit.Outline(b.transform, UIKit.Gold, "hud/panel_sharp_outline");
            var t1 = UIKit.Label(rt, "T", "عرض البداية", 50, Color.white, TextAnchor.MiddleRight, UIKit.Display);
            UIKit.Place(t1.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 4f), new Vector2(280f, 80f));
            var t2 = UIKit.Label(rt, "D", "٥٠٠ لؤلؤة + جنوط ذهبية + دخان علم الكويت", 36, new Color(1f, 0.95f, 0.85f), TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(t2.rectTransform, new Vector2(1f, 0.5f), new Vector2(-310f, 2f), new Vector2(560f, 64f));
            UIKit.Fit(t2, 26, 36);
            var pb = UIKit.Panel(rt, "Buy", UIKit.Gold, "hud/pill");
            pb.raycastTarget = false;
            UIKit.Place(pb.rectTransform, new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(230f, 76f));
            var pt = UIKit.Label(pb.transform, "T", IapStore.Price(IapStore.Starter, IapStore.StarterFallback), 38, new Color(0.12f, 0.08f, 0.03f), TextAnchor.MiddleCenter, UIKit.Black, false);
            UIKit.Stretch(pt.rectTransform, 8f, 8f, 0f, 0f);
            UIKit.Fit(pt, 24, 38);
        }

        void BuyStarter()
        {
            Sfx.Play("ui_click");
            IapStore.Buy(IapStore.Starter, (ok, msg) =>
            {
                if (!string.IsNullOrEmpty(msg)) flow.Toast(msg);
                if (ok) { Sfx.Play("ui_reward"); garage?.Refresh(); Show(view); }
            });
        }

        void OpenCat(Customs.Cat c)
        {
            Sfx.Play("ui_click");
            if (c == Customs.Cat.Engine || c == Customs.Cat.Turbo)
            {
                // performance stages live in the workshop's parts panel (cash only)
                Close();
                garage?.Open(WorkshopPanel.Mode.Parts);
                return;
            }
            cat = c;
            Show(View.Items);
        }

        public void OpenCategory(Customs.Cat c) => OpenCat(c);
        public void ShowPacks() => Show(View.Packs);
        public void TryOnForPreview(int i) => TryOn(i);

        // ------------------------------------------------------------------ items
        struct Row { public string ar; public bool cash; public long price; public bool owned, fitted; public Color swatch; public Texture2D tex; public string tile; }

        List<Row> Rows()
        {
            var rows = new List<Row>();
            switch (cat)
            {
                case Customs.Cat.Rims:
                {
                    int cur = PlayerPrefs.GetInt($"dk_rim_{car}", 0);
                    for (int i = 0; i < Tuning.Rims.Length; i++)
                    {
                        var r = Tuning.Rims[i];
                        rows.Add(new Row { ar = r.ar, cash = r.pearls == 0, price = r.pearls > 0 ? r.pearls : (i == 0 ? 0 : Tuning.RimPrice), owned = Tuning.OwnsRim(car, i), fitted = cur == i, swatch = i == 0 ? new Color(0.5f, 0.5f, 0.52f) : r.rim, tile = "tile_rims" });
                    }
                    break;
                }
                case Customs.Cat.Paint:
                {
                    int cur = PlayerPrefs.GetInt($"dk_paint_{car}", 0);
                    for (int i = 0; i < Tuning.Paints.Length; i++)
                    {
                        var pc = Tuning.Paints[i];
                        bool prem = i >= Tuning.CashPaints;
                        rows.Add(new Row { ar = pc.ar, cash = !prem, price = prem ? pc.pearls : (i == 0 ? 0 : Tuning.PaintPrice), owned = prem ? Tuning.OwnsPaint(i) : i == 0, fitted = cur == i, swatch = i == 0 ? new Color(0.25f, 0.25f, 0.27f) : pc.c });
                    }
                    break;
                }
                case Customs.Cat.Nitro:
                {
                    int lvl = Customs.Nitro(car);
                    for (int i = 0; i < Customs.NitroMax; i++)
                        rows.Add(new Row { ar = i == 0 ? "نيترو ١ · ٣ ثواني" : "نيترو ٢ · ٥ ثواني", cash = true, price = Customs.NitroPrice[i], owned = lvl > i, fitted = lvl > i, tile = "tile_nitro" });
                    break;
                }
                default:
                {
                    int cur = Customs.SavedFit(car, cat);
                    var list = Customs.Items[cat];
                    for (int i = 0; i < list.Length; i++)
                    {
                        var it = list[i];
                        rows.Add(new Row { ar = it.ar, price = it.pearls, owned = Customs.Owns(cat, i), fitted = cur == i, swatch = it.color,
                            tex = it.tex != null ? Resources.Load<Texture2D>("DriftKuwait/Customs/" + it.tex) : null,
                            tile = cat == Customs.Cat.Wing || cat == Customs.Cat.Fenders || cat == Customs.Cat.Exhaust ? Customs.CatTile(cat) : null });
                    }
                    break;
                }
            }
            return rows;
        }

        void BuildItems()
        {
            var head = UIKit.Label(body, "Cat", Customs.CatAr(cat), 54, UIKit.Gold, TextAnchor.MiddleRight, UIKit.Display);
            UIKit.Place(head.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, 0f), new Vector2(420f, 70f));
            var hint = UIKit.Label(body, "Hint", Customs.IsSpeed(cat) ? "تنشرى بالكاش اللي تكسبه من التقحيص" : "اضغط على أي واحد .. تشوفه على سيارتك قبل لا تشتري", 32,
                new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleRight, UIKit.Arabic);
            UIKit.Place(hint.rectTransform, new Vector2(1f, 1f), new Vector2(-440f, -8f), new Vector2(W - 500f, 56f));
            UIKit.Fit(hint, 24, 32);

            // scrolling grid of big cards, three across
            var vp = UIKit.Rect(body, "Viewport");
            vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one; vp.offsetMin = Vector2.zero; vp.offsetMax = new Vector2(0f, -84f);
            vp.gameObject.AddComponent<RectMask2D>();
            var vpi = vp.gameObject.AddComponent<Image>(); vpi.color = new Color(0, 0, 0, 0.01f);
            var content = UIKit.Rect(vp, "Content");
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            float cw = (W - 40f - 28f) / 3f;
            grid.cellSize = new Vector2(cw, 330f); grid.spacing = new Vector2(14f, 14f);
            grid.startCorner = GridLayoutGroup.Corner.UpperRight; grid.childAlignment = TextAnchor.UpperRight;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.viewport = vp; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;

            var rows = Rows();
            bool optional = !Customs.IsSpeed(cat) && cat != Customs.Cat.Rims && cat != Customs.Cat.Paint;
            for (int i = 0; i < rows.Count; i++) Card(content, rows[i], i, cw, optional);
        }

        void Card(RectTransform parent, Row r, int i, float cw, bool optional)
        {
            int idx = i;
            var card = UIKit.Button(parent, "Card" + i, r.fitted ? new Color(0.25f, 0.18f, 0.05f, 1f) : new Color(0.02f, 0.04f, 0.08f, 1f), () => TryOn(idx), "hud/panel_sharp");
            if (r.fitted) UIKit.Outline(card.transform, UIKit.Gold, "hud/panel_sharp_outline");
            var rt = (RectTransform)card.transform;
            // picture
            var pic = UIKit.Rect(rt, "Pic");
            pic.anchorMin = new Vector2(0f, 1f); pic.anchorMax = new Vector2(1f, 1f); pic.pivot = new Vector2(0.5f, 1f);
            pic.offsetMin = new Vector2(8f, -190f); pic.offsetMax = new Vector2(-8f, -8f);
            pic.gameObject.AddComponent<RectMask2D>();
            if (r.tex != null)
            {
                var bg = UIKit.Img(pic, "Bg", null, cat == Customs.Cat.Slogans ? new Color(0.02f, 0.02f, 0.02f, 1f) : new Color(0.3f, 0.32f, 0.36f, 1f));
                bg.raycastTarget = false; UIKit.Stretch(bg.rectTransform);
                var ri = new GameObject("Tex", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                ri.transform.SetParent(pic, false); ri.texture = r.tex; ri.raycastTarget = false;
                float a = r.tex.width / (float)r.tex.height;
                float bh = 170f, bw = Mathf.Min(cw - 30f, bh * a); bh = bw / a;
                UIKit.Place(ri.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(bw, bh));
            }
            else if (r.tile != null)
            {
                var tex = Resources.Load<Texture2D>("DriftKuwait/Customs/Tiles/" + r.tile);
                if (tex != null) { var ri = UIKit.Cover(pic, "Img", tex); ri.raycastTarget = false; if (cat != Customs.Cat.Nitro && cat != Customs.Cat.Wing && cat != Customs.Cat.Fenders) ri.color = Color.Lerp(Color.white, r.swatch, 0.55f); }
                if (cat == Customs.Cat.Rims) Swatch(pic, r.swatch);
            }
            else Swatch(pic, r.swatch);
            // name
            var nm = UIKit.Label(rt, "N", r.ar, 40, UIKit.Cream, TextAnchor.MiddleRight, UIKit.Black);
            UIKit.Place(nm.rectTransform, new Vector2(1f, 0f), new Vector2(-14f, 82f), new Vector2(cw - 28f, 52f));
            UIKit.Fit(nm, 26, 40); nm.raycastTarget = false;
            // price / state
            if (r.fitted) Tag(rt, "مركّب", UIKit.Gold);
            else if (r.owned) Tag(rt, "عندك", new Color(0.5f, 0.85f, 0.55f));
            else if (r.price > 0) PricePill(rt, r.cash, r.price, new Vector2(10f, -10f));
            // action
            string act = r.fitted ? (optional ? "شيله" : (cat == Customs.Cat.Nitro ? "مركّب" : "مركّب")) : r.owned ? "ركّبه" : "اشتري";
            bool enabled = !(r.fitted && !optional);
            var ab = UIKit.Button(rt, "Act", enabled ? (r.fitted ? UIKit.InkSoft : UIKit.Gold) : new Color(0.3f, 0.3f, 0.3f, 0.8f), () => { if (enabled) Act(idx); }, "hud/pill");
            UIKit.Place((RectTransform)ab.transform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(cw - 28f, 62f));
            var at = UIKit.Label(ab.transform, "T", act, 36, r.fitted ? UIKit.Cream : new Color(0.12f, 0.08f, 0.03f), TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(at.rectTransform);
        }

        static void Swatch(RectTransform pic, Color c)
        {
            var sw = UIKit.Img(pic, "Swatch", UIKit.S("hud/circle_mask"), c);
            sw.raycastTarget = false;
            UIKit.Place(sw.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
            var shine = UIKit.Img(sw.transform, "Shine", UIKit.S("hud/circle_mask"), new Color(1f, 1f, 1f, 0.22f));
            shine.raycastTarget = false;
            UIKit.Place(shine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-24f, 26f), new Vector2(54f, 54f));
        }

        static void Tag(RectTransform parent, string ar, Color c)
        {
            var t = UIKit.Label(parent, "Tag", ar, 30, c, TextAnchor.MiddleLeft, UIKit.Black);
            UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -14f), new Vector2(200f, 44f));
            UIKit.AddShadow(t, 2f, 0.9f);
            t.raycastTarget = false;
        }

        /// Shows an item on the car without buying it.
        void TryOn(int i)
        {
            Sfx.Play("ui_click");
            if (cat == Customs.Cat.Nitro) return;
            Customs.PreviewCar = car;
            if (cat == Customs.Cat.Rims) Tuning.PreviewRim = i;
            else if (cat == Customs.Cat.Paint) Tuning.PreviewPaint = i;
            else Customs.Preview[cat] = i;
            garage?.Refresh();
        }

        void Act(int i)
        {
            Sfx.Play("ui_click");
            var rows = Rows();
            var r = rows[i];
            bool ok;
            if (cat == Customs.Cat.Rims) ok = Tuning.SetRim(car, i);
            else if (cat == Customs.Cat.Paint) ok = Tuning.SetPaint(car, i);
            else if (cat == Customs.Cat.Nitro)
            {
                if (i != Customs.Nitro(car)) { flow.Toast(i < Customs.Nitro(car) ? "مركّب عندك" : "ركّب نيترو ١ أول"); return; }
                ok = Customs.BuyNitro(car);
                if (!ok) { flow.Toast("النيترو بـ " + UIKit.Dig(Customs.NitroPrice[i]) + " كاش .. قحّص وجمّع"); return; }
            }
            else ok = Customs.BuyAndFit(car, cat, r.fitted ? -1 : i);
            if (!ok)
            {
                if (r.cash) flow.Toast("ما يكفي الكاش .. قحّص وجمّع");
                else { flow.Toast("ما يكفي اللؤلؤ"); Show(View.Packs); }
                return;
            }
            if (!r.owned && r.price > 0) Sfx.Play("ui_reward");
            Customs.Preview.Remove(cat);
            if (cat == Customs.Cat.Rims) Tuning.PreviewRim = -1;
            if (cat == Customs.Cat.Paint) Tuning.PreviewPaint = -1;
            Tuning.RaiseChanged(car);
            garage?.Refresh();
            Show(View.Items);
        }

        // ------------------------------------------------------------------ pearl packs
        void BuildPacks()
        {
            var head = UIKit.Label(body, "Cat", "اللؤلؤ", 54, UIKit.Gold, TextAnchor.MiddleRight, UIKit.Display);
            UIKit.Place(head.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, 0f), new Vector2(300f, 70f));
            var hint = UIKit.Label(body, "Hint", "اللؤلؤ للشكل والستايل بس .. القوة بالتقحيص", 32, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleRight, UIKit.Arabic);
            UIKit.Place(hint.rectTransform, new Vector2(1f, 1f), new Vector2(-320f, -8f), new Vector2(W - 380f, 56f));
            UIKit.Fit(hint, 24, 32);
            float top = 86f;
            if (!Customs.StarterOwned) { StarterBanner(body, top); top += 122f; }
            float cw = (W - 40f - 42f) / 4f, ch = 420f;
            for (int i = 0; i < IapStore.Packs.Length; i++)
            {
                var p = IapStore.Packs[i];
                string id = p.id;
                var card = UIKit.Button(body, "Pack" + i, new Color(0.02f, 0.04f, 0.08f, 1f), () => BuyPack(id), "hud/panel_sharp");
                var rt = (RectTransform)card.transform;
                UIKit.Place(rt, new Vector2(1f, 1f), new Vector2(-i * (cw + 14f), -top), new Vector2(cw, ch));
                UIKit.Outline(card.transform, i == 2 ? UIKit.Gold : new Color(1f, 1f, 1f, 0.15f), "hud/panel_sharp_outline");
                // a little heap of pearls, bigger for bigger packs
                int n = 3 + i * 3;
                for (int k = 0; k < n; k++)
                {
                    float ang = k * 2.4f, rad = 14f + 9f * Mathf.Sqrt(k);
                    var pearl = UIKit.Img(rt, "P", UIKit.S("hud/circle_mask"), Color.Lerp(PearlCol, new Color(0.8f, 0.86f, 0.95f), (k % 3) / 3f));
                    pearl.raycastTarget = false;
                    UIKit.Place(pearl.rectTransform, new Vector2(0.5f, 1f), new Vector2(Mathf.Cos(ang) * rad, -110f + Mathf.Sin(ang) * rad * 0.6f), new Vector2(44f, 44f));
                }
                var amt = UIKit.Label(rt, "A", UIKit.Dig(p.pearls), 60, Color.white, TextAnchor.MiddleCenter, UIKit.Digital, false);
                UIKit.Place(amt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(cw - 20f, 70f));
                var lb = UIKit.Label(rt, "L", "لؤلؤة", 36, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
                UIKit.Place(lb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(cw - 20f, 50f));
                if (i == 2)
                {
                    var best = UIKit.Label(rt, "B", "الأوفر", 32, UIKit.Gold, TextAnchor.MiddleCenter, UIKit.Black);
                    UIKit.Place(best.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(cw - 20f, 44f));
                }
                var pb = UIKit.Panel(rt, "Buy", UIKit.Gold, "hud/pill");
                pb.raycastTarget = false;
                UIKit.Place(pb.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(cw - 30f, 76f));
                var pt = UIKit.Label(pb.transform, "T", IapStore.Price(p.id, p.fallback), 38, new Color(0.12f, 0.08f, 0.03f), TextAnchor.MiddleCenter, UIKit.Black, false);
                UIKit.Stretch(pt.rectTransform, 8f, 8f, 0f, 0f);
                UIKit.Fit(pt, 24, 38);
            }
            var rs = UIKit.Button(body, "Restore", UIKit.InkSoft, () =>
            {
                Sfx.Play("ui_click");
                IapStore.Restore((ok, msg) => { flow.Toast(msg); if (ok) { garage?.Refresh(); Show(view); } });
            }, "hud/pill");
            UIKit.Place((RectTransform)rs.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(380f, 72f));
            UIKit.Outline(rs.transform, new Color(1, 1, 1, 0.25f), "hud/pill");
            var rst = UIKit.Label(rs.transform, "T", "رجّع مشترياتي", 36, UIKit.Cream, TextAnchor.MiddleCenter, UIKit.Black);
            UIKit.Stretch(rst.rectTransform);
        }

        void BuyPack(string id)
        {
            Sfx.Play("ui_click");
            IapStore.Buy(id, (ok, msg) =>
            {
                if (!string.IsNullOrEmpty(msg)) flow.Toast(msg);
                if (ok) Sfx.Play("ui_reward");
                Refresh();
            });
        }
    }
}
