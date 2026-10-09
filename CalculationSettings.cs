using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace FluentPrayerTimes;

public sealed partial class MainWindow
{
    static readonly (int Id, string Ar)[] Methods = {
        (-1, "تلقائي حسب الموقع"), (5, "الهيئة العامة المصرية للمساحة"),
        (2, "الاتحاد الإسلامي بأمريكا الشمالية"), (3, "رابطة العالم الإسلامي"),
        (4, "جامعة أم القرى"), (1, "جامعة العلوم الإسلامية بكراتشي"),
        (12, "اتحاد المنظمات الإسلامية في فرنسا"), (8, "منطقة الخليج"),
        (9, "الكويت"), (10, "قطر"), (11, "سنغافورة"), (13, "تركيا"),
        (14, "روسيا"), (99, "تحديد يدوي")
    };
    void LoadCalculationUi()
    {
        bool was = _loading; _loading = true;
        CalculationControls.Children.Clear();
        void Label(string text) => CalculationControls.Children.Add(new TextBlock { Text = L.T(text), TextWrapping = TextWrapping.Wrap });
        void Save() { _s.Save(); _ = ReloadAsync(); }
        ComboBox Choice(string label, string[] items, int selected, Action<int> set)
        {
            Label(label);
            var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var item in items) combo.Items.Add(L.T(item));
            combo.SelectedIndex = selected;
            combo.SelectionChanged += (_, _) => { if (_loading || combo.SelectedIndex < 0) return; set(combo.SelectedIndex); Save(); LoadCalculationUi(); };
            CalculationControls.Children.Add(combo); return combo;
        }
        NumberBox Number(string label, double value, double min, double max, Action<double> set)
        {
            var box = new NumberBox { Header = L.T(label), Value = value, Minimum = min, Maximum = max, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            box.ValueChanged += (_, e) => { if (_loading || double.IsNaN(e.NewValue)) return; set(e.NewValue); Save(); };
            CalculationControls.Children.Add(box); return box;
        }
        Label("تُختار الإعدادات تلقائيًا حسب الموقع. يمكنك تعديل كل خيار أو إعادته إلى الوضع التلقائي.");
        string? methodName = _res?.Today == null ? null : Methods.FirstOrDefault(m => m.Id == _res.Today.Method).Ar;
        if (_s.CalculationMethod == -1 && methodName != null) Label(L.T("الطريقة المستخدمة: ") + L.T(methodName));
        Choice("المذهب الفقهي لصلاة العصر", new[] { "تلقائي حسب الموقع", "الشافعي والمالكي والحنبلي", "الحنفي" }, _s.AsrSchool + 1, i => _s.AsrSchool = i - 1);
        Choice("طريقة الحساب لصلاتي الفجر والعشاء", Methods.Select(m => m.Ar).ToArray(), Math.Max(0, Array.FindIndex(Methods, m => m.Id == _s.CalculationMethod)), i => _s.CalculationMethod = Methods[i].Id);
        if (_s.CalculationMethod == 99)
        {
            Choice("إعدادات الحساب اليدوي", new[] { "زوايا الفجر والعشاء", "زاوية الفجر ودقائق العشاء بعد المغرب", "دقائق الفجر قبل الشروق والعشاء بعد المغرب", "دقائق الفجر قبل الشروق وزاوية العشاء" }, _s.CustomRule, i => _s.CustomRule = i);
            if (_s.CustomRule is 0 or 1) Number("زاوية الفجر (درجة)", _s.FajrAngle, 1, 30, v => _s.FajrAngle = v);
            else Number("الفجر قبل الشروق (دقيقة)", _s.FajrMinutes, 1, 240, v => _s.FajrMinutes = v);
            if (_s.CustomRule is 0 or 3) Number("زاوية العشاء (درجة)", _s.IshaAngle, 1, 30, v => _s.IshaAngle = v);
            else Number("العشاء بعد المغرب (دقيقة)", _s.IshaMinutes, 1, 240, v => _s.IshaMinutes = v);
        }
        Choice("التعديل للمناطق ذات خطوط العرض المرتفعة", new[] { "تلقائي حسب الموقع", "لا تعديل", "منتصف الليل", "سبع الليلة", "طريقة الحساب بالزاوية" }, _s.HighLatitude + 1, i => _s.HighLatitude = i - 1);
        var zone = new TextBox { Header = L.T("المنطقة الزمنية"), PlaceholderText = L.T("تلقائي حسب الموقع"), Text = _s.TimeZoneOverride };
        var apply = new Button { Content = L.T("تطبيق") };
        apply.Click += (_, _) => {
            var name = zone.Text.Trim();
            try { if (name != "") TimeZoneInfo.FindSystemTimeZoneById(name); }
            catch { zone.Header = L.T("المنطقة الزمنية غير صحيحة"); return; }
            _s.TimeZoneOverride = name; Save();
        };
        CalculationControls.Children.Add(zone); CalculationControls.Children.Add(apply);
        RbDstOff.IsEnabled = RbDstOn.IsEnabled = _s.Location.Cc == "EG" && string.IsNullOrEmpty(_s.TimeZoneOverride);
        _loading = was;
    }
}
