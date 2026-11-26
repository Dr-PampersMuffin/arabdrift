#!/usr/bin/env python3
"""Builds the public pages the App Store listing links to (privacy policy, terms of use, support), each in English
and Arabic, into site/. They are published by GitHub Pages from the gh-pages branch:
  https://dr-pampersmuffin.github.io/arabdrift/privacy.html  (and terms.html, support.html)
The same URLs are in tools/asc/listing.json and in the game (Scripts/Online/Legal.cs).

Usage: python3 tools/site/build.py
"""
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "site")
DEV = "Embur"
EMAIL = "support@embur.world"
UPDATED_EN = "November 2026"
UPDATED_AR = "نوفمبر 2026"
EULA = "https://www.apple.com/legal/internet-services/itunes/dev/stdeula/"
APPLE_PRIVACY = "https://www.apple.com/legal/privacy/"
REFUND = "https://reportaproblem.apple.com"

CONTACT_EN = f'Email <a href="mailto:{EMAIL}">{EMAIL}</a>. We answer within three working days.'
CONTACT_AR = f'راسلنا على <a href="mailto:{EMAIL}">{EMAIL}</a>. نردّ خلال ثلاثة أيام عمل.'


def page(title, en, ar):
    return f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>{title}</title>
<style>body{{font:18px/1.65 -apple-system,system-ui,sans-serif;max-width:780px;margin:0 auto;padding:24px 16px;color:#2b1d12;background:#faf5ea}}
h1{{font-size:28px}}h2{{font-size:20px;margin-top:28px}}a{{color:#8a4b12}}nav{{font-size:16px;margin-bottom:12px}}
.ar{{direction:rtl;text-align:right;border-top:1px solid #d8c9ad;margin-top:44px;padding-top:12px;font-size:19px}}
footer{{margin-top:40px;font-size:14px;color:#7a6650}}
@media (prefers-color-scheme:dark){{body{{background:#17120d;color:#efe4d2}}a{{color:#e0a85c}}.ar{{border-color:#4a3b2a}}footer{{color:#a8977f}}}}</style></head><body>
<nav><a href="privacy.html">Privacy</a> · <a href="terms.html">Terms of Use</a> · <a href="support.html">Support</a> · <a href="#ar">العربية</a></nav>
{en}
<div class="ar" id="ar" lang="ar">{ar}</div>
<footer>© 2026 Embur. Q8 Drift (تقحيص).</footer>
</body></html>
"""


PRIVACY = page(
    "Q8 Drift: Privacy Policy",
    f"""<h1>Q8 Drift: Privacy Policy</h1>
<p>Last updated: {UPDATED_EN}. Q8 Drift (تقحيص, "the game") is made by {DEV} ("we"). This policy explains what the game keeps, where, and your choices.</p>
<h2>The short version</h2>
<p>We do not collect any personal data. The game has no account, no sign-up, no advertising, no analytics and no tracking. We do not run servers that receive your data.</p>
<h2>What stays on your iPhone</h2>
<ul><li><b>Game progress:</b> your story progress, stars, cash, pearls, cars, parts, looks and settings are saved only on your iPhone, inside the app. They are included in your own iPhone or iCloud backups if you use them. We never receive them.</li>
<li><b>Motion sensor:</b> if you choose tilt steering, the game reads the iPhone's motion sensor while you drive, only to steer. Nothing is stored or sent.</li></ul>
<h2>Apple services</h2>
<ul><li><b>Game Center (optional):</b> if you are signed in to Game Center, your scores are sent to Apple's Game Center leaderboards, where other players can see your Game Center nickname and score. This is run by Apple under <a href="{APPLE_PRIVACY}">Apple's Privacy Policy</a>; we do not receive your Apple Account details.</li>
<li><b>In-app purchases:</b> purchases are processed by Apple. We never see your card, payment or Apple Account details. The game only learns which item was bought so it can give it to you.</li></ul>
<h2>What we do not do</h2>
<ul><li>No advertising, no ad tracking, no selling or sharing of data with anyone.</li>
<li>No access to your location, contacts, photos, camera or microphone.</li></ul>
<h2>Children</h2><p>The game does not collect personal data from anyone, including children. Parents can turn off or limit in-app purchases with Screen Time.</p>
<h2>Deleting your data</h2><p>Deleting the app removes all game data from your iPhone. Pearls already bought are stored on the iPhone and cannot be brought back after deleting the app; the one-time Starter Pack can be restored with "Restore purchases" (رجّع مشترياتي). To remove Game Center scores, manage your Game Center profile in the iPhone's Settings.</p>
<h2>Changes</h2><p>If this policy changes, we will update this page and its date.</p>
<h2>Contact</h2><p>{CONTACT_EN}</p>""",
    f"""<h1>تقحيص (Q8 Drift): سياسة الخصوصية</h1>
<p>آخر تحديث: {UPDATED_AR}. لعبة تقحيص ("اللعبة") من صنع {DEV} ("نحن"). توضّح هذه السياسة ما تحفظه اللعبة وأين، وما خياراتك.</p>
<h2>باختصار</h2>
<p>لا نجمع أي بيانات شخصية. لا يوجد في اللعبة حساب ولا تسجيل ولا إعلانات ولا تحليلات ولا تتبّع، ولا نشغّل خوادم تستقبل بياناتك.</p>
<h2>ما يبقى على جهازك</h2>
<ul><li><b>تقدّمك في اللعبة:</b> مراحل القصة والنجوم والكاش واللؤلؤ والسيارات والقطع والتعديلات والإعدادات تُحفظ على جهازك فقط داخل اللعبة، وتدخل في نسخك الاحتياطية الخاصة على الجهاز أو iCloud إن كنت تستخدمها. لا تصلنا أبداً.</li>
<li><b>مستشعر الحركة:</b> إذا اخترت التحكم بالميلان، تقرأ اللعبة مستشعر الحركة أثناء القيادة للتوجيه فقط، ولا يُحفظ شيء ولا يُرسل.</li></ul>
<h2>خدمات Apple</h2>
<ul><li><b>Game Center (اختياري):</b> إذا كنت مسجّلاً في Game Center تُرسل نتائجك إلى لوحات الترتيب في Game Center، ويرى اللاعبون الآخرون لقبك ونتيجتك. تديرها Apple وفق <a href="{APPLE_PRIVACY}">سياسة خصوصية Apple</a>، ولا تصلنا بيانات حساب Apple الخاص بك.</li>
<li><b>المشتريات داخل اللعبة:</b> تتولاها Apple، ولا نرى بطاقتك أو بيانات الدفع أو حساب Apple. تعرف اللعبة فقط ما الذي اشتريته لتسلّمه لك.</li></ul>
<h2>ما لا نفعله</h2>
<ul><li>لا إعلانات، ولا تتبّع إعلاني، ولا بيع أو مشاركة لأي بيانات مع أي جهة.</li>
<li>لا وصول إلى موقعك أو جهات اتصالك أو صورك أو الكاميرا أو الميكروفون.</li></ul>
<h2>الأطفال</h2><p>اللعبة لا تجمع بيانات شخصية من أي أحد، ومنهم الأطفال. يمكن للوالدين إيقاف المشتريات أو تقييدها عبر مدة استخدام الجهاز.</p>
<h2>حذف بياناتك</h2><p>حذف اللعبة يزيل كل بياناتها من جهازك. اللؤلؤ المشترى محفوظ على الجهاز ولا يمكن استرجاعه بعد حذف اللعبة، أما عرض البداية (يُشترى مرة واحدة) فيُسترجع من "رجّع مشترياتي". لإزالة نتائج Game Center، أدِر ملفك في Game Center من إعدادات الجهاز.</p>
<h2>التغييرات</h2><p>إن تغيّرت هذه السياسة نحدّث هذه الصفحة وتاريخها.</p>
<h2>التواصل</h2><p>{CONTACT_AR}</p>""",
)

TERMS = page(
    "Q8 Drift: Terms of Use",
    f"""<h1>Q8 Drift: Terms of Use (EULA)</h1>
<p>Last updated: {UPDATED_EN}. These terms are between you and {DEV}, the maker of Q8 Drift (تقحيص). Your use of the app is governed by
<a href="{EULA}">Apple's Standard Licensed Application End User License Agreement</a> (the "Standard EULA"), together with these terms. Where these terms and the Standard EULA differ, the Standard EULA applies.</p>
<h2>1. The game</h2><p>Q8 Drift is a driving game set in Kuwait in 1987. Its story, characters, police and events are fictional, and any likeness to real people is a coincidence. The game is played on your iPhone; Game Center is optional.</p>
<h2>2. Drive safely in real life</h2><p>Drifting, racing and running from the police belong in the game only. Never copy them on real roads. Do not play while driving.</p>
<h2>3. Pearls and purchases</h2>
<ul><li>Pearls (لؤلؤ) and the items bought with them (paint, rims, stickers, slogans, body kits, wings, exhausts, tyre smoke, neon) are a limited licence to use virtual content inside the game. They have no cash value, cannot be exchanged for money, and cannot be transferred or sold outside the game.</li>
<li>Pearls change only how a car looks. Parts that make a car faster (engine, turbo, nitro) are bought only with cash earned by playing.</li>
<li>Every price is shown before you buy. There are no random or loot-box items.</li>
<li>Pearl packs are consumable and are stored on your iPhone; they are not restored after deleting the app. The Starter Pack is a one-time purchase and can be restored with "Restore purchases" (رجّع مشترياتي).</li>
<li>Purchases are processed by Apple. Refund requests go to Apple at <a href="{REFUND}">reportaproblem.apple.com</a>.</li>
<li>We may change, balance or retire in-game content and prices as the game evolves.</li></ul>
<h2>4. Fair play</h2><p>Do not use modified apps, cheats or bugs to change scores sent to Game Center leaderboards. We may ask Apple to remove scores that are clearly not genuine.</p>
<h2>5. Availability</h2><p>We work to keep the game working well but cannot promise it will be free of errors. Features and content may change in updates.</p>
<h2>6. Liability</h2><p>To the extent the law allows, the game is provided "as is", and our liability is limited as set out in the Standard EULA.</p>
<h2>7. Changes</h2><p>We may update these terms; the date above shows the latest version. Continuing to play means you accept the update.</p>
<h2>8. Contact</h2><p>{CONTACT_EN}</p>""",
    f"""<h1>تقحيص (Q8 Drift): شروط الاستخدام (اتفاقية الترخيص)</h1>
<p>آخر تحديث: {UPDATED_AR}. هذه الشروط بينك وبين {DEV}، صانع لعبة تقحيص. ويخضع استخدامك للتطبيق
<a href="{EULA}">لاتفاقية ترخيص المستخدم النهائي القياسية من Apple</a> إلى جانب هذه الشروط. عند أي اختلاف بينهما، تُطبَّق الاتفاقية القياسية.</p>
<h2>1. اللعبة</h2><p>تقحيص لعبة قيادة في الكويت عام 1987. قصتها وشخصياتها والشرطة والأحداث فيها خيالية، وأي تشابه مع أشخاص حقيقيين هو مصادفة. تُلعب على جهازك، وGame Center اختياري.</p>
<h2>2. القيادة الآمنة في الواقع</h2><p>التقحيص والسباق والهروب من الشرطة مكانها اللعبة فقط. لا تقلّدها في الشوارع الحقيقية أبداً، ولا تلعب وأنت تقود.</p>
<h2>3. اللؤلؤ والمشتريات</h2>
<ul><li>اللؤلؤ وما يُشترى به (الصبغ والجنوط والملصقات والعبارات والرفارف والجناح والأكزوز ودخان الكفرات والنيون) ترخيص محدود لاستخدام محتوى افتراضي داخل اللعبة. ليست له قيمة نقدية، ولا يُستبدل بمال، ولا يُنقل أو يُباع خارج اللعبة.</li>
<li>اللؤلؤ يغيّر شكل السيارة فقط. القطع التي تزيد السرعة (المكينة والتيربو والنيترو) تُشترى فقط بالكاش الذي تجمعه من اللعب.</li>
<li>يظهر كل سعر قبل الشراء. لا صناديق عشوائية.</li>
<li>حزم اللؤلؤ تُستهلك وتُحفظ على جهازك، ولا تُسترجع بعد حذف اللعبة. عرض البداية يُشترى مرة واحدة ويُسترجع من "رجّع مشترياتي".</li>
<li>تتولى Apple معالجة المشتريات، وطلبات الاسترداد تُرسل إلى Apple عبر <a href="{REFUND}">reportaproblem.apple.com</a>.</li>
<li>قد نغيّر محتوى اللعبة وأسعارها أو نوازنها أو نوقفها مع تطوّر اللعبة.</li></ul>
<h2>4. اللعب النظيف</h2><p>لا تستخدم تطبيقات معدّلة أو غشاً أو أخطاء لتغيير النتائج المرسلة إلى لوحات ترتيب Game Center. قد نطلب من Apple إزالة النتائج غير الحقيقية بوضوح.</p>
<h2>5. التوفّر</h2><p>نعمل لتعمل اللعبة جيداً، لكن لا نضمن خلوّها من الأخطاء. قد تتغيّر الميزات والمحتوى في التحديثات.</p>
<h2>6. المسؤولية</h2><p>بالقدر الذي يسمح به القانون، تُقدَّم اللعبة "كما هي"، وتكون مسؤوليتنا محدودة وفق الاتفاقية القياسية.</p>
<h2>7. التغييرات</h2><p>قد نحدّث هذه الشروط، والتاريخ أعلاه يبيّن أحدث نسخة. استمرارك في اللعب يعني قبولك للتحديث.</p>
<h2>8. التواصل</h2><p>{CONTACT_AR}</p>""",
)

SUPPORT = page(
    "Q8 Drift: Support",
    f"""<h1>Q8 Drift: Support</h1>
<p>{CONTACT_EN} Please include your iPhone model and, if you can, a screenshot.</p>
<h2>Common questions</h2>
<p><b>A purchase did not arrive.</b> Close and reopen the game with an internet connection; unfinished purchases are delivered automatically. If it is still missing, write to us with the date and the item.</p>
<p><b>I changed iPhone. Is my progress safe?</b> Progress is saved on the iPhone. Moving to a new iPhone with an iPhone or iCloud backup brings it with you. The Starter Pack can always be restored: Garage, التعديل, the pearls page, رجّع مشترياتي (or Settings, رجّع مشترياتي).</p>
<p><b>I want a refund.</b> Refunds are handled by Apple at <a href="{REFUND}">reportaproblem.apple.com</a>.</p>
<p><b>The car is stuck.</b> Pause, then رجّع السيارة للشارع puts it back on the nearest road.</p>
<p><b>How do I drift?</b> Pause, then طريقة اللعب shows the controls. Turn up مساعدة التقحيص (drift assist) in Settings for an easier slide.</p>
<p><b>How do I stop purchases on my child's iPhone?</b> Use Screen Time, Content &amp; Privacy Restrictions, iTunes &amp; App Store Purchases.</p>
<p>Policies: <a href="privacy.html">Privacy Policy</a> · <a href="terms.html">Terms of Use</a> · <a href="{EULA}">Apple Standard EULA</a></p>""",
    f"""<h1>تقحيص (Q8 Drift): الدعم</h1>
<p>{CONTACT_AR} اذكر نوع جهازك، وأرفق صورة للشاشة إن أمكن.</p>
<h2>أسئلة شائعة</h2>
<p><b>لم تصل عملية شراء.</b> أغلق اللعبة وافتحها مع اتصال بالإنترنت؛ تُسلَّم المشتريات غير المكتملة تلقائياً. إن بقيت مفقودة راسلنا مع التاريخ والمنتج.</p>
<p><b>غيّرت جهازي، هل تقدّمي محفوظ؟</b> التقدّم محفوظ على الجهاز، والانتقال إلى جهاز جديد بنسخة احتياطية من الجهاز أو iCloud ينقله معك. عرض البداية يُسترجع دائماً: الكراج، التعديل، صفحة اللؤلؤ، رجّع مشترياتي (أو الإعدادات، رجّع مشترياتي).</p>
<p><b>أريد استرداد المبلغ.</b> الاسترداد تتولاه Apple عبر <a href="{REFUND}">reportaproblem.apple.com</a>.</p>
<p><b>السيارة علقت.</b> أوقف اللعبة مؤقتاً ثم اختر "رجّع السيارة للشارع".</p>
<p><b>كيف أقحّص؟</b> أوقف اللعبة مؤقتاً ثم "طريقة اللعب" تشرح التحكم. ارفع "مساعدة التقحيص" من الإعدادات لتقحيص أسهل.</p>
<p><b>كيف أمنع المشتريات في جهاز طفلي؟</b> من مدة استخدام الجهاز، قيود المحتوى والخصوصية، مشتريات iTunes وApp Store.</p>
<p>السياسات: <a href="privacy.html">سياسة الخصوصية</a> · <a href="terms.html">شروط الاستخدام</a> · <a href="{EULA}">اتفاقية Apple القياسية</a></p>""",
)

INDEX = page(
    "Q8 Drift",
    """<h1>Q8 Drift (تقحيص)</h1><p>A drifting game set in Kuwait, 1987, by Embur.</p>
<p><a href="privacy.html">Privacy Policy</a> · <a href="terms.html">Terms of Use</a> · <a href="support.html">Support</a></p>""",
    """<h1>تقحيص</h1><p>لعبة تقحيص في الكويت عام 1987، من Embur.</p>
<p><a href="privacy.html">سياسة الخصوصية</a> · <a href="terms.html">شروط الاستخدام</a> · <a href="support.html">الدعم</a></p>""",
)

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, html in (("privacy", PRIVACY), ("terms", TERMS), ("support", SUPPORT), ("index", INDEX)):
        with open(os.path.join(OUT, name + ".html"), "w", encoding="utf-8") as f:
            f.write(html)
    open(os.path.join(OUT, ".nojekyll"), "w").close()
    print("site/ written")
