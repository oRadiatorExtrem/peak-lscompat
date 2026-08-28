#!/usr/bin/env python
"""Generate evidence figures for the README from PeakTimeDiag CSVs.

Usage:
    python tools/plot_evidence.py <PeakTimeDiag.csv> <PeakTimeDiag2.csv>

Outputs docs/*.png. Run with Python 3 + matplotlib.
"""
import csv
import sys

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt

WINDOW_MIN = 18  # both sessions overlap over the first 18 minutes


def load(path):
    rows = []
    with open(path, newline="") as f:
        for r in csv.DictReader(f):
            try:
                row = {k: float(v) for k, v in r.items()}
                if row["wallSec"] > 0:
                    rows.append(row)
            except (ValueError, KeyError, TypeError):
                pass
    return rows


FLOOR = 0.05  # log-axis floor; measured 0 Hz values are plotted at this floor


def hz(v):
    return max(v, FLOOR)


def fig_fixed_hz(bug, fixed, out):
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(11, 4.2), sharey=True)

    b = [r for r in bug if r["wallSec"] <= WINDOW_MIN * 60]
    ax1.plot([r["wallSec"] / 60 for r in b], [hz(r["fixedHz"]) for r in b],
             color="#d62728", lw=1.4)
    ax1.set_yscale("log")
    ax1.set_ylim(FLOOR, 200)
    ax1.set_title("Without plugin (LSFG active)", fontsize=11)
    ax1.set_xlabel("wall-clock time (min)")
    ax1.set_ylabel("FixedUpdate rate (Hz, log scale)")
    ax1.axhline(60, color="#2ca02c", ls="--", lw=1, alpha=0.7)
    ax1.annotate("expected 60 Hz", xy=(0.4, 66), color="#2ca02c", fontsize=9)
    ax1.annotate("physics collapses to ~0.2 Hz\n(frames still render at ~55 fps)",
                 xy=(2.2, 0.3), xytext=(3.2, 6), fontsize=9,
                 arrowprops=dict(arrowstyle="->", lw=0.8))

    f_all = [r for r in fixed if r["wallSec"] <= WINDOW_MIN * 60]
    f_foc = [r for r in f_all if r.get("focused", 1) == 1]
    ax2.plot([r["wallSec"] / 60 for r in f_all], [hz(r["fixedHz"]) for r in f_all],
             color="#bbbbbb", lw=1.0, label="window unfocused")
    ax2.plot([r["wallSec"] / 60 for r in f_foc], [hz(r["fixedHz"]) for r in f_foc],
             color="#1f77b4", lw=1.4, label="window focused")
    ax2.set_yscale("log")
    ax2.set_ylim(FLOOR, 200)
    ax2.set_title("With plugin (maxDeltaTime fix, LSFG active)", fontsize=11)
    ax2.set_xlabel("wall-clock time (min)")
    ax2.axhline(60, color="#2ca02c", ls="--", lw=1, alpha=0.7)
    ax2.legend(fontsize=8, loc="lower left")

    fig.suptitle("PEAK + Lossless Scaling LSFG: physics (FixedUpdate) rate "
                 "measured with PeakTimeDiag (1 sample/s; 0 Hz values "
                 "plotted at the 0.05 Hz axis floor)", fontsize=12)
    fig.tight_layout(rect=(0, 0, 1, 0.94))
    fig.savefig(out, dpi=150)
    plt.close(fig)


def fig_drift(bug, fixed, out):
    fig, ax = plt.subplots(figsize=(8, 4.2))

    b = [r for r in bug if r["wallSec"] <= WINDOW_MIN * 60]
    ax.plot([r["wallSec"] / 60 for r in b],
            [r["wallSec"] - r["time"] for r in b],
            color="#d62728", lw=1.4, label="without plugin (game lost 465 s in 18 min)")

    f_all = [r for r in fixed if r["wallSec"] <= WINDOW_MIN * 60]
    f_foc = [r for r in f_all if r.get("focused", 1) == 1]
    ax.plot([r["wallSec"] / 60 for r in f_all],
            [r["wallSec"] - r["time"] for r in f_all],
            color="#bbbbbb", lw=1.0, label="with plugin, window unfocused (focus loss)")
    ax.plot([r["wallSec"] / 60 for r in f_foc],
            [r["wallSec"] - r["time"] for r in f_foc],
            color="#1f77b4", lw=1.4, label="with plugin, window focused")

    ax.set_xlabel("wall-clock time (min)")
    ax.set_ylabel("simulation clock lag behind real time (s)")
    ax.set_title("PEAK + LSFG: simulation clock drift "
                 "(a flat line = game runs at real speed)")
    ax.legend(fontsize=9)
    fig.tight_layout()
    fig.savefig(out, dpi=150)
    plt.close(fig)


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(1)
    bug, fixed = load(sys.argv[1]), load(sys.argv[2])
    fig_fixed_hz(bug, fixed, "docs/fixedupdate-rate.png")
    fig_drift(bug, fixed, "docs/clock-drift.png")
    print("wrote docs/fixedupdate-rate.png, docs/clock-drift.png")


if __name__ == "__main__":
    main()
