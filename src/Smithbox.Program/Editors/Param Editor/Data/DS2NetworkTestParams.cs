using Andre.Formats;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;

namespace StudioCore.Editors.ParamEditor;

/// <summary>
/// The bounded parameter layouts needed to inspect NPJB90625's map. Unknown bytes remain opaque;
/// accepting a table requires its observed big-endian header, data version, type and row size.
/// </summary>
public static class DS2NetworkTestParams
{
    private readonly record struct Layout(string Type, ushort Version, int RowSize);

    private static readonly Dictionary<string, Layout> Layouts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EnemyParam"] = new("CHR_PARAM", 36, 320),
        ["generatorregistparam"] = new("GENERATOR_REGIST_PARAM", 7, 16),
        ["generatorlocation"] = new("GENERATOR_LOCATION_PARAM", 2, 32),
        ["generatorparam"] = new("GENERATOR_PARAM", 13, 128),
        ["eventparam"] = new("EVENT_PARAM", 2, 16),
        ["eventlocation"] = new("GENERAL_LOCATION_EVENT_PARAM_ST", 1, 48),
        ["mapobjectinstanceparam"] = new("MAP_OBJECT_INSTANCE_PARAM", 4, 56)
    };

    private static string GetLayoutName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName.Replace('\\', '/'));
        if (name.Equals("EnemyParam", StringComparison.OrdinalIgnoreCase))
            return "EnemyParam";

        var mapSuffix = name.IndexOf("_m", StringComparison.OrdinalIgnoreCase);
        if (mapSuffix < 0)
            return "";

        // Per-map keys have the same mAA_BB_CC_DD suffix used by MapUniverse.
        var mapId = name[(mapSuffix + 1)..];
        if (mapId.Length != 12 || mapId[3] != '_' || mapId[6] != '_' || mapId[9] != '_')
            return "";
        for (var i = 1; i < mapId.Length; i++)
        {
            if (i != 3 && i != 6 && i != 9 && !char.IsAsciiDigit(mapId[i]))
                return "";
        }
        return name[..mapSuffix];
    }

    public static bool IsSupported(string fileName) => Layouts.ContainsKey(GetLayoutName(fileName));

    public static string GetKey(string fileName)
    {
        if (!IsSupported(fileName))
            throw new InvalidDataException($"Unsupported PS3 Network Test parameter: {fileName}");
        var name = Path.GetFileNameWithoutExtension(fileName.Replace('\\', '/'));
        return name.Equals("EnemyParam", StringComparison.OrdinalIgnoreCase) ? "EnemyParam" : name.ToLowerInvariant();
    }

    public static Param Read(string fileName, Memory<byte> bytes, IReadOnlyDictionary<string, PARAMDEF> definitions)
    {
        if (!Layouts.TryGetValue(GetLayoutName(fileName), out var layout))
            throw new InvalidDataException($"Unsupported PS3 Network Test parameter: {fileName}");

        var param = Param.Read(bytes);
        if (!param.BigEndian || param.ParamdefFormatVersion != 255 || param.ParamType != layout.Type ||
            param.ParamdefDataVersion != layout.Version || (param.Rows.Count != 0 && param.RowSize != layout.RowSize))
        {
            throw new InvalidDataException($"Unexpected PS3 Network Test layout in {fileName}: " +
                $"type {param.ParamType}, data version {param.ParamdefDataVersion}, row size {param.RowSize}, " +
                $"format {param.ParamdefFormatVersion}, big endian {param.BigEndian}. Expected " +
                $"{layout.Type}, version {layout.Version}, {layout.RowSize} bytes, format 255, big endian.");
        }

        if (!definitions.TryGetValue(layout.Type, out var definition) || definition.DataVersion != layout.Version)
            throw new InvalidDataException($"Missing PS3 Network Test definition for {layout.Type} version {layout.Version}.");

        if (param.Rows.Count == 0)
        {
            // An empty PARAM has no row offsets from which to infer a stride. Its header still
            // has to match the known NT layout above; use only that layout's verified definition.
            // This also permits editor-only inspection projects to suppress unrelated event tables.
            var empty = new Param(definition, param.BigEndian, 0)
            {
                Format2D = param.Format2D,
                Format2E = param.Format2E,
                ParamdefFormatVersion = param.ParamdefFormatVersion,
                ParamdefDataVersion = param.ParamdefDataVersion,
                Unk06 = param.Unk06,
                ParamType = param.ParamType
            };
            if (empty.RowSize != layout.RowSize)
                throw new InvalidDataException($"Unexpected empty-table definition size in {fileName}: {empty.RowSize}; expected {layout.RowSize}.");
            return empty;
        }

        param.ApplyParamdef(definition, 0, fileName);
        return param;
    }
}
