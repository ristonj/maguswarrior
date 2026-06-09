using System;
using System.Collections.Generic;

namespace MagusWarrior.Hex;

public readonly record struct HexCoord(int Q, int R) {
    public static readonly HexCoord[] Directions = {
        new( 1,  0), new(-1,  0),
        new( 0,  1), new( 0, -1),
        new( 1, -1), new(-1,  1),
    };

    public static HexCoord operator +(HexCoord a, HexCoord b) =>
        new(a.Q + b.Q, a.R + b.R);

    public IEnumerable<HexCoord> Neighbors() {
        foreach (var d in Directions)
            yield return this + d;
    }

    public int Distance(HexCoord other) {
        int dx = Q - other.Q;
        int dz = R - other.R;
        int dy = -dx - dz;
        return (Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz)) / 2;
    }
}
