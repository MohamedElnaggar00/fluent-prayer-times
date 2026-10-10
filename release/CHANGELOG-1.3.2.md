## سجل التغييرات - الإصدار 1.3.2

### الإصلاحات
- إصلاح بقاء بطاقة «مواقيت الصلاة» في مشغّل وسائط ويندوز بعد انتهاء صوت الأذان أو الإقامة. كان النظام يسجّل جلسة وسائط عند تشغيل الصوت ولا تُغلق بعد انتهائه، فتبقى البطاقة ظاهرة باسم مؤقت. لن يظهر البرنامج في مشغّل الوسائط بعد الآن.

### التنزيلات
- النسخة المحمولة: فك الضغط وشغّل البرنامج. تتطلب .NET 8 وVisual C++ وWindows App Runtime 1.6 بنظام x64.
- المثبت الكبير: يتضمن بيئة تشغيله الخاصة لبدء التثبيت دون تثبيت .NET مسبقًا.
- المثبت الصغير المعتمد على .NET: يفحص المتطلبات ويثبّت الناقص من Microsoft بواجهة تثبيت أصلية.
- يضيف المثبتان اختصارات إلى قائمة ابدأ وسطح المكتب. يلزم اتصال بالإنترنت للمتطلبات الناقصة وصلاحيات المسؤول.
- يدعم Windows 10 1809 والإصدارات الأحدث وWindows 11 بنظام x64. البرنامج غير موقّع، وقد يعرض Windows SmartScreen تحذيرًا.

## Fluent Prayer Times 1.3.2

### Fixed
- The Windows media player no longer shows a stuck "Fluent Prayer Times" card after the adhan or iqama sound ends. Windows used to register a media session for the sound that was never closed, so the card stayed visible under a temporary name. The app no longer appears in the media player at all.

### Downloads
- Portable ZIP: extract and run. Requires .NET 8 x64, Visual C++ x64 and Windows App Runtime 1.6.
- Self-contained installer: includes its own .NET runtime so setup can start without a preinstalled runtime.
- Small .NET-runtime-dependent installer: native setup checks and installs missing requirements from Microsoft.
- Both installers add Start menu and desktop shortcuts. Missing dependencies require an internet connection and administrator approval.
- Windows 10 1809+ / Windows 11, x64. Unsigned application: Windows SmartScreen may show a warning.
