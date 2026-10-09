## Fluent Prayer Times 1.3.0

### Added
- Location-based prayer calculation defaults with independent manual overrides.
- Calculation methods for Egypt, North America, Muslim World League, Umm al-Qura, Karachi, France and other regional authorities.
- Asr school selection, custom Fajr/Isha angles and minute offsets, and high-latitude adjustments.
- Optional time-zone override.

### Fixed
- Worldwide location selection no longer assumes Egyptian cities when requesting prayer times.
- Calculation settings are included in the offline cache key to prevent stale timings after an option changes.
- Improved alignment of the desktop widget's prayer name, time and heading.
- Iqama countdowns show whole minutes only, rounded up, without seconds.
- Installers check the x64 .NET 8 runtime folder, install the correct Microsoft.NETCore.App runtime and verify Microsoft download signatures.
- The small runtime-dependent installer uses a native front end that can start on a PC without .NET installed.
- Windows App Runtime detection includes all users.
- Installing or updating while the app is running in the system tray no longer fails with "Setup was unable to automatically close all applications". Both installers now close the running app automatically and start it again in the tray after an update. Uninstalling closes it too.

## سجل التغييرات - الإصدار 1.3.0

### الإضافات
- اختيار إعدادات حساب المواقيت تلقائيًا حسب الموقع، مع إمكانية تعديل كل خيار يدويًا.
- طرق حساب معتمدة لمصر وأمريكا الشمالية ورابطة العالم الإسلامي وأم القرى وكراتشي وفرنسا وجهات إقليمية أخرى.
- اختيار المذهب لحساب صلاة العصر، وتحديد زوايا الفجر والعشاء أو الفواصل الزمنية يدويًا، وخيارات تعديل المواقيت لخطوط العرض المرتفعة.
- إمكانية تحديد المنطقة الزمنية يدويًا.

### الإصلاحات
- لم تعد طلبات المواقيت تفترض أن المدينة المختارة تقع في مصر.
- فصل المواقيت المحفوظة وفق إعدادات الحساب، لمنع عرض بيانات قديمة بعد تعديل الخيارات.
- تحسين محاذاة اسم الصلاة ووقتها والعنوان في ويدجت سطح المكتب.
- عرض العد التنازلي للإقامة بالدقائق فقط، مع تقريب الجزء المتبقي من الدقيقة إلى الأعلى.
- فحص مجلد بيئة التشغيل .NET 8 بنظام x64، وتنزيل Microsoft.NETCore.App الصحيح، والتحقق من التوقيع الرقمي لتنزيلات Microsoft.
- واجهة تثبيت أصلية للنسخة الصغيرة المعتمدة على .NET، يمكنها بدء التشغيل حتى عند عدم تثبيت .NET مسبقًا.
- فحص Windows App Runtime لجميع المستخدمين.
- لم يعد التثبيت أو التحديث أثناء تشغيل البرنامج في شريط النظام يفشل برسالة "Setup was unable to automatically close all applications". يغلق المثبتان الآن البرنامج تلقائيًا ويعيدان تشغيله في شريط النظام بعد التحديث، كما يغلقه إلغاء التثبيت.

### Downloads / التنزيلات
- Portable ZIP: extract and run. Requires .NET 8 x64, Visual C++ x64 and Windows App Runtime 1.6.
- Self-contained installer: includes its own .NET runtime so setup can start without a preinstalled runtime.
- Small .NET-runtime-dependent installer: native setup checks and installs missing requirements from Microsoft.
- Both installers add Start menu and desktop shortcuts. Missing dependencies require an internet connection and administrator approval.
- Windows 10 1809+ / Windows 11, x64. Unsigned application: Windows SmartScreen may show a warning.

- النسخة المحمولة: فك الضغط وشغّل البرنامج. تتطلب .NET 8 وVisual C++ وWindows App Runtime 1.6 بنظام x64.
- المثبت الكبير: يتضمن بيئة تشغيله الخاصة لبدء التثبيت دون تثبيت .NET مسبقًا.
- المثبت الصغير المعتمد على .NET: يفحص المتطلبات ويثبّت الناقص من Microsoft بواجهة تثبيت أصلية.
- يضيف المثبتان اختصارات إلى قائمة ابدأ وسطح المكتب. يلزم اتصال بالإنترنت للمتطلبات الناقصة وصلاحيات المسؤول.
- يدعم Windows 10 1809 والإصدارات الأحدث وWindows 11 بنظام x64. البرنامج غير موقّع، وقد يعرض Windows SmartScreen تحذيرًا.
