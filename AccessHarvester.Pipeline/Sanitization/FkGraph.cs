using System;
using System.Collections.Generic;
using AccessHarvester.Pipeline.Classification;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// Per-column remap decisions produced by <see cref="FkGraphBuilder"/>. The
/// graph captures the two facts the masking engine needs to honour the FK
/// consistency invariant (plan § 5):
///
/// 1. <see cref="PkRemapDecisions"/> — for every PK column, whether its
///    values will be remapped during sanitization. A PK is remapped when
///    its effective masking strategy is <c>remap-id</c>.
/// 2. <see cref="FkEffectiveStrategies"/> — for every FK column, the
///    forced effective strategy (<c>preserve</c> when the referenced PK is
///    preserved, <c>remap-id</c> when the referenced PK is remapped). The
///    schema's <c>allOf</c> rule already constrains human overrides on FK
///    columns to these two values; the graph derives the value mechanically
///    from the PK decision so the engine never has to second-guess.
///
/// Both dictionaries are keyed case-insensitively to mirror Access identifier
/// semantics.
/// </summary>
public sealed record FkGraph(
    IReadOnlyDictionary<TableColumn, bool> PkRemapDecisions,
    IReadOnlyDictionary<TableColumn, MaskingStrategy> FkEffectiveStrategies);
