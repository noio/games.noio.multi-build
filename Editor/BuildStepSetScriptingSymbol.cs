using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace noio.MultiBuild
{
[Serializable]
[BuildStep(BuildStepOrder.PreBuild, true)]
public class BuildStepSetScriptingSymbol : BuildStep
{
    [SerializeField] string _symbol = "SCRIPTING_DEFINE_SYMBOL";
    [SerializeField] bool _define = true;
    public override string DisplayName => $"{(_define ? "Set" : "Remove")} Symbol '{_symbol}'";

    public override void Validate(BuildConfig buildConfig, List<BuildStepMessage> messages)
    {
        if (IsDefineSet(buildConfig.Targets, _symbol, _define) == false)
        {
            messages.Add(new BuildStepMessage(Severity.Warning,
                $"Symbol '{_symbol}' is currently {(_define ? "not " : "")}defined. It will be " +
                $"{(_define ? "set" : "removed")} before building, but since that causes code to be " +
                $"compiled differently, compile errors may result.",
                fixAction: () => ToggleDefine(buildConfig.Targets, _symbol, _define),
                fixActionName: _define? "Set Symbol" : "Remove Symbol"));
        }
    }

    public override void Apply(
        BuildConfig buildConfig,
        BuildOptionWrapper options
    )
    {
        ToggleDefine(buildConfig.Targets, _symbol, _define);
    }

    //
    // protected override BuildStepResult Check(BuildConfig buildConfig)
    // {
    //     if (IsDefineSet(buildConfig.Targets, _symbol, _define) == false)
    //     {
    //         return new BuildStepResult(BuildStepResult.ResultType.Warning,
    //             $"Symbol '{_symbol}' is currently {(_define ? "not" : "")} defined. It will be set " +
    //             $"before building, but since that causes code to be compiled differently, compile " +
    //             $"errors may result. Use 'Apply Steps Only' to set the symbol now.");
    //     }
    //
    //     return default;
    // }

    void ToggleDefine(IEnumerable<BuildTarget> targets, string defineSymbol, bool value)
    {
        var namedBuildTargets = targets.Select(BuildPipeline.GetBuildTargetGroup)
                                       .Select(NamedBuildTarget.FromBuildTargetGroup)
                                       .Distinct();

        foreach (var namedBuildTarget in namedBuildTargets)
        {
            PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget, out var d);
            var defines = d.ToList();
            if (value)
            {
                if (defines.Contains(defineSymbol) == false)
                {
                    defines.Add(defineSymbol);
                }
            }
            else
            {
                defines.Remove(defineSymbol);
            }

            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, defines.ToArray());
        }
    }

    bool IsDefineSet(IEnumerable<BuildTarget> targets, string defineSymbol, bool value)
    {
        var namedBuildTargets = targets.Select(BuildPipeline.GetBuildTargetGroup)
                                       .Select(NamedBuildTarget.FromBuildTargetGroup)
                                       .Distinct();
        return namedBuildTargets.All(g =>
        {
            PlayerSettings.GetScriptingDefineSymbols(g, out var defines);
            return defines.Contains(defineSymbol) == value;
        });
    }
}
}