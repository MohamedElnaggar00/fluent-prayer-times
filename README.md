# 🕌 Fluent Prayer Times
### برنامج مواقيت الصلاه - WinUI 3

مواقيت الصلاة بتصميم Fluent حقيقي (WinUI 3): خلفية Mica Alt، شريط تنقل جانبي، كروت وراديو وتوجلز بلون teal، مع أيقونة في الـ tray وعدّاد تنازلي للصلاة القادمة.

**المطور: محمد النجار** · *Vibe coding with Instinct.AI* · الإصدار 1.0

## ✨ المميزات
- أيقونة مأذنة في الـ tray: كليك يفتح/يقفل النافذة، كليك يمين: فتح / الإعدادات / خروج، وtooltip مباشر بالعدّاد
- عدّاد تنازلي + تمييز الصلاة القادمة (بعد العشاء بيعرض فجر الغد)
- التاريخ الميلادي + الهجري، الأوقات بنظام 12 ساعة
- 31 مدينة مصرية + بحث عن أي مكان + إحداثيات يدوية + تحديد الموقع تلقائيًا
- التوقيت الصيفي: تلقائي / إيقاف (UTC+2) / تشغيل (UTC+3)
- **ويدجت سطح المكتب**: نافذة صغيرة عايمة بالصلاة القادمة والعدّاد، تتحرك بالسحب وتتثبت فوق النوافذ (من الإعدادات أو كليك يمين على أيقونة الـ tray)
- المظهر: Mica Alt / Mica / Acrylic، وسمة فاتح / غامق / حسب النظام
- التشغيل مع ويندوز، يشتغل أوفلاين (كاش محلي)، نسخة واحدة فقط
- المواقيت: Aladhan API (طريقة الحساب 5 - الهيئة المصرية العامة للمساحة)

## 📦 المتطلبات
- Windows 10 (1809+) / Windows 11، x64
- **Windows App Runtime 1.6 (x64)** - [تحميل وتثبيت مرة واحدة](https://aka.ms/windowsappsdk/1.6/latest/windowsappruntimeinstall-x64.exe)

## ▶️ التشغيل
حمّل الـ zip من **Releases**، فك الضغط، شغّل `FluentPrayerTimes.exe`. البرنامج غير موقّع: لو ظهر SmartScreen اضغط **More info ثم Run anyway**.

## 🛠️ البناء
البناء بيتم على GitHub Actions (`windows-latest`) لأن WinUI 3 محتاج Windows:

```powershell
dotnet publish FluentPrayerTimes.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained false -o publish
```

## 🚀 إصدار نسخة جديدة
الـ workflow في `.github/workflows/release.yml` بيبني، يتأكد من هيكل الناتج، يجرب تشغيل البرنامج، يعمل zip وينشر Release.

- **tag:** `git tag v1.1.0 && git push origin v1.1.0`
- **يدويًا:** `Actions` ← `Build and Release` ← `Run workflow` ← رقم الإصدار

> نص "الإصدار: 1.0" في صفحة "عن البرنامج" مكتوب في `MainWindow.xaml`.

## 📄 الترخيص
مشروع شخصي. كل الحقوق محفوظة © محمد النجار - Vibe coding with Instinct.AI
