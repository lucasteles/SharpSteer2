// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace SharpSteer2.Demo;

public abstract class PlugIn : IPlugIn
{
    public abstract void Open();
    public abstract void Update(float currentTime, float elapsedTime);
    public abstract void Redraw(float currentTime, float elapsedTime);
    public abstract void Close();
    public abstract string Name { get; }
    public abstract IEnumerable<IVehicle> Vehicles { get; }

    // prototypes for function pointers used with PlugIns
    public delegate void PlugInCallBackFunction(PlugIn clientObject);
    public delegate void VoidCallBackFunction();
    public delegate void TimeStepCallBackFunction(float currentTime, float elapsedTime);

    protected readonly IAnnotationService annotations;

    // constructor
    protected PlugIn(IAnnotationService annotations)
    {
        this.annotations = annotations;

        // save this new instance in the registry
        AddToRegistry();
    }

    // default reset method is to do a close then an open
    public virtual void Reset()
    {
        Close();
        Open();
    }

    // default sort key (after the "built ins")
    public virtual float SelectionOrderSortKey => 1.0f;

    // default is to NOT request to be initially selected
    public virtual bool RequestInitialSelection => false;

    // default function key handler: ignore all
    public virtual void HandleFunctionKeys(Keys key) { }

    // default "mini help": print nothing
    public virtual void PrintMiniHelpForFunctionKeys() { }

    // returns pointer to the next PlugIn in "selection order"
    public PlugIn Next()
    {
        for (var i = 0; i < itemsInRegistry; i++)
        {
            if (this != registry[i])
                continue;

            var atEnd = i == itemsInRegistry - 1;
            return registry[atEnd ? 0 : i + 1];
        }
        return null;
    }

    // format instance to characters for printing to stream
    public override string ToString() => $"<PlugIn \"{Name}\">";

    // CLASS FUNCTIONS

    // search the class registry for a Plugin with the given name
    public static IPlugIn FindByName(string name)
    {
        if (string.IsNullOrEmpty(name) == false)
        {
            for (var i = 0; i < itemsInRegistry; i++)
            {
                var pi = registry[i];
                var s = pi.Name;
                if (string.IsNullOrEmpty(s) && name == s)
                    return pi;
            }
        }
        return null;
    }

    // apply a given function to all PlugIns in the class registry
    public static void ApplyToAll(PlugInCallBackFunction f)
    {
        for (var i = 0; i < itemsInRegistry; i++)
        {
            f(registry[i]);
        }
    }

    // sort PlugIn registry by "selection order"
    public static void SortBySelectionOrder()
    {
        // I know, I know, just what the world needs:
        // another inline shell sort implementation...

        // starting at each of the first n-1 elements of the array
        for (var i = 0; i < itemsInRegistry - 1; i++)
        {
            // scan over subsequent pairs, swapping if larger value is first
            for (var j = i + 1; j < itemsInRegistry; j++)
            {
                var iKey = registry[i].SelectionOrderSortKey;
                var jKey = registry[j].SelectionOrderSortKey;

                if (iKey > jKey)
                {
                    var temporary = registry[i];
                    registry[i] = registry[j];
                    registry[j] = temporary;
                }
            }
        }
    }

    // returns pointer to default PlugIn (currently, first in registry)
    public static PlugIn FindDefault()
    {
        // return NULL if no PlugIns exist
        if (itemsInRegistry == 0) return null;

        // otherwise, return the first PlugIn that requests initial selection
        for (var i = 0; i < itemsInRegistry; i++)
        {
            if (registry[i].RequestInitialSelection) return registry[i];
        }

        // otherwise, return the "first" PlugIn (in "selection order")
        return registry[0];
    }

    // save this instance in the class's registry of instances
    void AddToRegistry() =>
        // save this instance in the registry
        registry[itemsInRegistry++] = this;

    // This array stores a list of all PlugIns.  It is manipulated by the
    // constructor and destructor, and used in findByName and applyToAll.
    const int TotalSizeOfRegistry = 1000;
    static int itemsInRegistry;
    static readonly PlugIn[] registry = new PlugIn[TotalSizeOfRegistry];
}
