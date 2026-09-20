"""Original, deterministic street blockout. All dimensions are centimetres."""

from dataclasses import dataclass


@dataclass(frozen=True)
class Box:
    name: str
    center: tuple[float, float, float]
    size: tuple[float, float, float]
    material: str
    solid: bool = True


MATERIALS = {
    "Asphalt": ((0.065, 0.07, 0.075), 0.91, 0.0),
    "Paving": ((0.37, 0.39, 0.37), 0.86, 0.0),
    "Brick": ((0.25, 0.12, 0.085), 0.83, 0.0),
    "Plaster": ((0.54, 0.55, 0.51), 0.87, 0.0),
    "Copper": ((0.08, 0.24, 0.20), 0.46, 0.65),
    "Window": ((0.08, 0.13, 0.16), 0.19, 0.0),
    "Paint": ((0.72, 0.71, 0.59), 0.72, 0.0),
    "Iron": ((0.075, 0.08, 0.075), 0.43, 0.85),
}

SPAWN = (-4000.0, -730.0, 120.0)


def build_plan():
    boxes = [Box("Street", (0, 0, -25), (12000, 6400, 50), "Asphalt")]
    for side in (-1, 1):
        # Twelve-metre carriageway, three-metre sidewalks, twelve-centimetre kerb.
        boxes.append(Box(f"Sidewalk_{side}", (0, side * 750, 6), (12000, 300, 12), "Paving"))
        for index, x in enumerate((-3900, -1300, 1300, 3900)):
            height = (1800, 2400, 2100, 1500)[index]
            prefix = f"Building_{side}_{index}"
            boxes.append(Box(prefix, (x, side * 1600, height / 2), (2200, 1400, height), "Brick" if index % 2 else "Plaster"))
            boxes.append(Box(prefix + "_Roof", (x, side * 1600, height + 15), (2240, 1440, 30), "Copper"))
            for level in range(1, height // 300):
                for column in range(6):
                    boxes.append(Box(f"{prefix}_Window_{level}_{column}",
                                     (x - 850 + column * 340, side * 897, level * 300 + 95),
                                     (145, 6, 170), "Window", False))
            # Seats leave a clear pedestrian corridor next to the kerb.
            boxes.append(Box(prefix + "_Bench", (x, side * 835, 35), (170, 45, 46), "Iron"))
        for index, x in enumerate(range(-5000, 5001, 1000)):
            boxes.append(Box(f"Bollard_{side}_{index}", (x, side * 625, 45), (18, 18, 90), "Iron"))
    for index, x in enumerate(range(-5600, 5601, 600)):
        boxes.append(Box(f"Lane_{index}", (x, 0, 0.4), (300, 12, 0.8), "Paint", False))
    # Physical blockout boundaries remain visible from both sides.
    for side in (-1, 1):
        boxes.append(Box(f"BoundaryX_{side}", (side * 5980, 0, 110), (40, 6400, 220), "Paving"))
        boxes.append(Box(f"BoundaryY_{side}", (0, side * 3180, 110), (12000, 40, 220), "Paving"))
    return boxes
