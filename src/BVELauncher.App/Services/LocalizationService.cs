// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace BVELauncher.App.Services;

public sealed class LocalizationService
{
    private readonly ResourceManager manager = new();
    private ResourceContext context;
    public string Language { get; private set; }
    public event Action? Changed;
    public LocalizationService()
    {
        context = manager.CreateResourceContext();
        Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "ja-JP" : "en-US";
        context.QualifierValues["Language"] = Language;
    }
    public string this[string key] => manager.MainResourceMap.GetValue("Resources/" + key, context).ValueAsString;
    public string Get(string key) => this[key];
    public string Format(string key, params object[] args) => string.Format(CultureInfo.GetCultureInfo(Language), this[key], args);
    public void SetLanguage(string language)
    {
        Language = language == "en-US" ? "en-US" : "ja-JP";
        context = manager.CreateResourceContext(); context.QualifierValues["Language"] = Language;
        Changed?.Invoke();
    }
}

