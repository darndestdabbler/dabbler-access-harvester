using System;
using System.Collections.Generic;

namespace AccessHarvester.Vba.Resolver.References;

// Curated static registry of VBA built-in identifiers the reference
// resolver consults as the third fallback (after scope-walking and the
// Access object model registry). Per the spec's Standards: "the list
// lives in source as a static registry, not loaded at runtime."
//
// Population strategy:
// - Every name that Phase 3.3 of the parser-migration plan calls out
//   explicitly (Format, IIf, Nz) is included.
// - Every bare-name use in the canonical Session-1 fixtures (02, 03,
//   09, 08-unresolvable.bas) that should obviously resolve here rather
//   than to Unresolved is included.
// - Anything ambiguous — names that might be either a built-in or a
//   user declaration — is omitted in favor of false-Unresolved over
//   false-resolved. Future sessions widen the registry conservatively.
//
// Keys are case-insensitive (VBA name comparison is ordinal
// case-insensitive), matching the rest of the resolver's naming
// behavior via Declarations.Declaration.NameComparer.
public static class BuiltInFunctionRegistry
{
    private static readonly Dictionary<string, BuiltInFunction> _byName
        = BuildRegistry();

    public static IReadOnlyDictionary<string, BuiltInFunction> All => _byName;

    public static BuiltInFunction? TryGet(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        return _byName.TryGetValue(name, out var fn) ? fn : null;
    }

    private static Dictionary<string, BuiltInFunction> BuildRegistry()
    {
        var d = new Dictionary<string, BuiltInFunction>(StringComparer.OrdinalIgnoreCase);

        void Add(string name, BuiltInCategory cat, string? returnType)
            => d[name] = new BuiltInFunction(name, cat, returnType);

        // ── String functions ─────────────────────────────────────────
        Add("Asc",       BuiltInCategory.String, "Integer");
        Add("AscB",      BuiltInCategory.String, "Integer");
        Add("AscW",      BuiltInCategory.String, "Integer");
        Add("Chr",       BuiltInCategory.String, "String");
        Add("ChrB",      BuiltInCategory.String, "String");
        Add("ChrW",      BuiltInCategory.String, "String");
        Add("Format",    BuiltInCategory.String, "String");
        Add("InStr",     BuiltInCategory.String, "Variant");
        Add("InStrRev",  BuiltInCategory.String, "Long");
        Add("LCase",     BuiltInCategory.String, "String");
        Add("Left",      BuiltInCategory.String, "String");
        Add("LeftB",     BuiltInCategory.String, "String");
        Add("Len",       BuiltInCategory.String, "Long");
        Add("LenB",      BuiltInCategory.String, "Long");
        Add("LTrim",     BuiltInCategory.String, "String");
        Add("Mid",       BuiltInCategory.String, "String");
        Add("MidB",      BuiltInCategory.String, "String");
        Add("Replace",   BuiltInCategory.String, "String");
        Add("Right",     BuiltInCategory.String, "String");
        Add("RightB",    BuiltInCategory.String, "String");
        Add("RTrim",     BuiltInCategory.String, "String");
        Add("Space",     BuiltInCategory.String, "String");
        Add("Split",     BuiltInCategory.String, "Variant");
        Add("StrComp",   BuiltInCategory.String, "Integer");
        Add("StrConv",   BuiltInCategory.String, "String");
        Add("String",    BuiltInCategory.String, "String");
        Add("StrReverse",BuiltInCategory.String, "String");
        Add("Trim",      BuiltInCategory.String, "String");
        Add("UCase",     BuiltInCategory.String, "String");

        // ── Math functions ───────────────────────────────────────────
        Add("Abs", BuiltInCategory.Math, null); // polymorphic numeric
        Add("Atn", BuiltInCategory.Math, "Double");
        Add("Cos", BuiltInCategory.Math, "Double");
        Add("Exp", BuiltInCategory.Math, "Double");
        Add("Fix", BuiltInCategory.Math, null);
        Add("Int", BuiltInCategory.Math, null);
        Add("Log", BuiltInCategory.Math, "Double");
        Add("Rnd", BuiltInCategory.Math, "Single");
        Add("Round", BuiltInCategory.Math, null);
        Add("Sgn", BuiltInCategory.Math, "Integer");
        Add("Sin", BuiltInCategory.Math, "Double");
        Add("Sqr", BuiltInCategory.Math, "Double");
        Add("Tan", BuiltInCategory.Math, "Double");

        // ── Date / Time functions ────────────────────────────────────
        Add("Date",       BuiltInCategory.DateTime, "Date");
        Add("DateAdd",    BuiltInCategory.DateTime, "Date");
        Add("DateDiff",   BuiltInCategory.DateTime, "Long");
        Add("DatePart",   BuiltInCategory.DateTime, "Variant");
        Add("DateSerial", BuiltInCategory.DateTime, "Date");
        Add("DateValue",  BuiltInCategory.DateTime, "Date");
        Add("Day",        BuiltInCategory.DateTime, "Integer");
        Add("Hour",       BuiltInCategory.DateTime, "Integer");
        Add("Minute",     BuiltInCategory.DateTime, "Integer");
        Add("Month",      BuiltInCategory.DateTime, "Integer");
        Add("MonthName",  BuiltInCategory.DateTime, "String");
        Add("Now",        BuiltInCategory.DateTime, "Date");
        Add("Second",     BuiltInCategory.DateTime, "Integer");
        Add("Time",       BuiltInCategory.DateTime, "Date");
        Add("TimeSerial", BuiltInCategory.DateTime, "Date");
        Add("TimeValue",  BuiltInCategory.DateTime, "Date");
        Add("Timer",      BuiltInCategory.DateTime, "Single");
        Add("Weekday",    BuiltInCategory.DateTime, "Integer");
        Add("WeekdayName",BuiltInCategory.DateTime, "String");
        Add("Year",       BuiltInCategory.DateTime, "Integer");

        // ── Information / type-test functions ────────────────────────
        Add("IsArray",   BuiltInCategory.Information, "Boolean");
        Add("IsDate",    BuiltInCategory.Information, "Boolean");
        Add("IsEmpty",   BuiltInCategory.Information, "Boolean");
        Add("IsError",   BuiltInCategory.Information, "Boolean");
        Add("IsMissing", BuiltInCategory.Information, "Boolean");
        Add("IsNothing", BuiltInCategory.Information, "Boolean");
        Add("IsNull",    BuiltInCategory.Information, "Boolean");
        Add("IsNumeric", BuiltInCategory.Information, "Boolean");
        Add("IsObject",  BuiltInCategory.Information, "Boolean");
        Add("TypeName",  BuiltInCategory.Information, "String");
        Add("VarType",   BuiltInCategory.Information, "Integer");

        // ── Conversion functions ─────────────────────────────────────
        Add("CBool", BuiltInCategory.Conversion, "Boolean");
        Add("CByte", BuiltInCategory.Conversion, "Byte");
        Add("CCur",  BuiltInCategory.Conversion, "Currency");
        Add("CDate", BuiltInCategory.Conversion, "Date");
        Add("CDbl",  BuiltInCategory.Conversion, "Double");
        Add("CDec",  BuiltInCategory.Conversion, "Variant");
        Add("CInt",  BuiltInCategory.Conversion, "Integer");
        Add("CLng",  BuiltInCategory.Conversion, "Long");
        Add("CLngLng",BuiltInCategory.Conversion, "LongLong");
        Add("CLngPtr",BuiltInCategory.Conversion, "LongPtr");
        Add("CSng",  BuiltInCategory.Conversion, "Single");
        Add("CStr",  BuiltInCategory.Conversion, "String");
        Add("CVar",  BuiltInCategory.Conversion, "Variant");
        Add("CVErr", BuiltInCategory.Conversion, "Variant");
        Add("Hex",   BuiltInCategory.Conversion, "String");
        Add("Oct",   BuiltInCategory.Conversion, "String");
        Add("Str",   BuiltInCategory.Conversion, "String");
        Add("Val",   BuiltInCategory.Conversion, "Double");

        // ── IO / file functions ──────────────────────────────────────
        Add("Dir",      BuiltInCategory.IO, "String");
        Add("EOF",      BuiltInCategory.IO, "Boolean");
        Add("FileLen",  BuiltInCategory.IO, "Long");
        Add("FreeFile", BuiltInCategory.IO, "Integer");
        Add("Loc",      BuiltInCategory.IO, "Long");
        Add("LOF",      BuiltInCategory.IO, "Long");

        // ── Interaction (host-level) functions ───────────────────────
        Add("InputBox", BuiltInCategory.Interaction, "String");
        Add("MsgBox",   BuiltInCategory.Interaction, "Integer");
        Add("Shell",    BuiltInCategory.Interaction, "Double");

        // ── Polymorphic combinators (Phase 3.3 anchors) ──────────────
        Add("IIf",    BuiltInCategory.Information, null);
        Add("Switch", BuiltInCategory.Information, null);
        Add("Choose", BuiltInCategory.Information, null);

        // ── Access-specific top-level identifiers ────────────────────
        // These appear as bare-name uses in fixtures. #11's registry
        // can override or augment them; the curated entries here
        // ensure Session 1's resolver does not produce a misleading
        // Unresolved("not-in-scope") for canonical Access surface.
        Add("Nz",            BuiltInCategory.AccessSpecific, null);
        Add("DLookup",       BuiltInCategory.AccessSpecific, "Variant");
        Add("DCount",        BuiltInCategory.AccessSpecific, "Variant");
        Add("DSum",           BuiltInCategory.AccessSpecific, "Variant");
        Add("DAvg",           BuiltInCategory.AccessSpecific, "Variant");
        Add("DMin",           BuiltInCategory.AccessSpecific, "Variant");
        Add("DMax",           BuiltInCategory.AccessSpecific, "Variant");
        Add("DFirst",         BuiltInCategory.AccessSpecific, "Variant");
        Add("DLast",          BuiltInCategory.AccessSpecific, "Variant");
        Add("Eval",           BuiltInCategory.AccessSpecific, "Variant");
        Add("CurrentDb",      BuiltInCategory.AccessSpecific, "Object");
        Add("CurrentProject", BuiltInCategory.AccessSpecific, "Object");
        Add("CurrentUser",    BuiltInCategory.AccessSpecific, "String");
        Add("DoCmd",          BuiltInCategory.AccessSpecific, "Object");
        Add("Application",    BuiltInCategory.AccessSpecific, "Object");
        Add("Forms",          BuiltInCategory.AccessSpecific, "Object");
        Add("Reports",        BuiltInCategory.AccessSpecific, "Object");
        Add("Screen",         BuiltInCategory.AccessSpecific, "Object");

        return d;
    }
}
