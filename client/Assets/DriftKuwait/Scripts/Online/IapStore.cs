using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

namespace DriftKuwait
{
    /// Real-money purchases through Apple (Unity In-App Purchasing): pearl packs (consumable) and the one-time starter
    /// pack (non-consumable, restorable). Products are created in App Store Connect by tools/asc/iap.py.
    public class IapStore : IDetailedStoreListener
    {
        public const string Pre = "world.embur.arabdrift.";
        public static readonly (string id, int pearls, string fallback)[] Packs =
        {
            (Pre + "pearls_100", 100, "$0.99"),
            (Pre + "pearls_550", 550, "$4.99"),
            (Pre + "pearls_1200", 1200, "$9.99"),
            (Pre + "pearls_2600", 2600, "$19.99"),
        };
        public const string Starter = Pre + "starter_pack";
        public const string StarterFallback = "$1.99";

        static IapStore inst;
        static IStoreController ctl;
        static IExtensionProvider ext;
        static Action<bool, string> pending;
        static bool restoring;   // a restore gives back the items, never the pearls again

        public static bool Ready => ctl != null;

        public static void Init()
        {
            if (inst != null) return;
            inst = new IapStore();
            try
            {
                var b = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
                foreach (var p in Packs) b.AddProduct(p.id, ProductType.Consumable);
                b.AddProduct(Starter, ProductType.NonConsumable);
                UnityPurchasing.Initialize(inst, b);
            }
            catch (Exception e) { Debug.LogWarning("[IAP] init failed: " + e.Message); }
        }

        /// The store's local price ("3.990 KD", "$4.99"), or the US price until the store answers.
        public static string Price(string id, string fallback)
        {
            if (ctl == null) return fallback;
            var p = ctl.products.WithID(id);
            return p != null && p.availableToPurchase && !string.IsNullOrEmpty(p.metadata.localizedPriceString) ? p.metadata.localizedPriceString : fallback;
        }

        /// Starts a purchase; done(true, message) once the pearls / items are in.
        public static void Buy(string id, Action<bool, string> done)
        {
            if (ctl == null) { Init(); done?.Invoke(false, "المتجر مو جاهز .. جرّب بعد شوي"); return; }
            var p = ctl.products.WithID(id);
            if (p == null || !p.availableToPurchase) { done?.Invoke(false, "هالعرض مو متوفر الحين"); return; }
            pending = done;
            ctl.InitiatePurchase(p);
        }

        /// Restores the starter pack on a new phone (Apple requires a restore button for one-time purchases).
        public static void Restore(Action<bool, string> done)
        {
            if (ext == null) { done?.Invoke(false, "المتجر مو جاهز .. جرّب بعد شوي"); return; }
            restoring = true;
            ext.GetExtension<IAppleExtensions>().RestoreTransactions((ok, msg) =>
            {
                restoring = false;
                done?.Invoke(ok, ok ? (Customs.StarterOwned ? "رجّعنا مشترياتك" : "ما في مشتريات نرجّعها") : "ما قدرنا نرجّع المشتريات");
            });
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            ctl = controller; ext = extensions;
            Debug.Log("[IAP] ready, products " + controller.products.all.Length);
        }

        public void OnInitializeFailed(InitializationFailureReason error) => Debug.LogWarning("[IAP] init failed: " + error);
        public void OnInitializeFailed(InitializationFailureReason error, string message) => Debug.LogWarning("[IAP] init failed: " + error + " " + message);

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e)
        {
            string id = e.purchasedProduct.definition.id;
            string msg = null;
            foreach (var p in Packs)
                if (p.id == id) { Customs.AddPearls(p.pearls); msg = "وصلتك " + UIKit.Num(p.pearls) + " لؤلؤة"; }
            if (id == Starter)
            {
                bool first = !Customs.StarterOwned && !restoring;
                Customs.GrantStarter(first);
                msg = first ? "عرض البداية صار لك!" : "رجّعنا عرض البداية";
            }
            Debug.Log("[IAP] granted " + id);
            var cb = pending; pending = null;
            cb?.Invoke(msg != null, msg ?? "");
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason) => Fail(reason.ToString());
        public void OnPurchaseFailed(Product product, PurchaseFailureDescription d) => Fail(d.reason.ToString());

        static void Fail(string why)
        {
            Debug.Log("[IAP] purchase failed: " + why);
            var cb = pending; pending = null;
            cb?.Invoke(false, why == "UserCancelled" ? "" : "ما تمت العملية .. ما انخصم شي");
        }
    }
}
