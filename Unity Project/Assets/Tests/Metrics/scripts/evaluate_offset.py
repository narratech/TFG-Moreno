import os
import json
import glob
import pandas as pd
import matplotlib.pyplot as plt

def process_single_offset_telemetry(data_dir, graphics_dir, json_filename):
    json_path = os.path.join(data_dir, json_filename)

    # Extraer el sufijo X (ej. TelemetryData1.json -> "1", TelemetryData.json -> "")
    suffix = json_filename[len("TelemetryData"):-5]

    # Cargar datos JSON
    with open(json_path, "r", encoding="utf-8") as f:
        frames = json.load(f)

    if not frames:
        print(f"File '{json_filename}' is empty.")
        return

    records = []

    # Procesar frames
    for frame in frames:
        time_stamp = frame.get("timeStamp", 0.0)
        std_agents = frame.get("standardAgents", [])
        ecs_agents = frame.get("ecsAgents", [])

        # Extraer offsetPercentage (%)
        std_offsets = [a["offsetPercentage"] * 100 for a in std_agents if "offsetPercentage" in a]
        ecs_offsets = [a["offsetPercentage"] * 100 for a in ecs_agents if "offsetPercentage" in a]
        total_offsets = std_offsets + ecs_offsets

        avg_std = sum(std_offsets) / len(std_offsets) if std_offsets else 0.0
        avg_ecs = sum(ecs_offsets) / len(ecs_offsets) if ecs_offsets else 0.0
        avg_total = sum(total_offsets) / len(total_offsets) if total_offsets else 0.0

        records.append({
            "TimeStamp": time_stamp,
            "Offset_Standard_Avg": avg_std,
            "Offset_ECS_Avg": avg_ecs,
            "Offset_Total_Avg": avg_total,
            "Count_Standard": len(std_offsets),
            "Count_ECS": len(ecs_offsets)
        })

    df = pd.DataFrame(records)

    # Resumen en consola
    print(f"\n--- Processing: {json_filename} ---")
    print(f"=== OFFSET STATISTICAL SUMMARY (%) ({json_filename}) ===")
    print(df[["Offset_Total_Avg", "Offset_Standard_Avg", "Offset_ECS_Avg"]].describe())

    # Crear gráfica
    plt.figure(figsize=(12, 6))
    plt.plot(df["TimeStamp"], df["Offset_Total_Avg"], label="Total Mean Offset (%)", color="purple", linewidth=2.5)

    if df["Count_Standard"].sum() > 0:
        plt.plot(df["TimeStamp"], df["Offset_Standard_Avg"], label="NavAgent (Standard)", color="blue", linestyle="--")
    if df["Count_ECS"].sum() > 0:
        plt.plot(df["TimeStamp"], df["Offset_ECS_Avg"], label="AgentComponent (ECS)", color="green", linestyle="--")

    plt.title(f"Mean Offset Percentage Over Time ({json_filename})", fontsize=14)
    plt.xlabel("Simulation Time (seconds)", fontsize=12)
    plt.ylabel("Mean Offset (%)", fontsize=12)
    plt.ylim(0, 105)
    plt.grid(True, linestyle=":", alpha=0.6)
    plt.legend(loc="upper right")

    plt.tight_layout()

    # Guardar gráfica en graphics_dir con el sufijo correspondiente
    output_image = os.path.join(graphics_dir, f"offset_telemetry{suffix}.png")
    plt.savefig(output_image, dpi=300)
    plt.close()  # Libera memoria entre procesamientos

    print(f"Plot saved to: {output_image}")


def process_offset_telemetry():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    data_dir = os.path.abspath(os.path.join(script_dir, "..", "data"))
    graphics_dir = os.path.abspath(os.path.join(script_dir, "..", "graphics"))

    if not os.path.exists(data_dir):
        print(f"Error: Data directory not found at '{data_dir}'")
        return

    # Buscar todos los archivos TelemetryData*.json
    search_pattern = os.path.join(data_dir, "TelemetryData*.json")
    files = glob.glob(search_pattern)

    if not files:
        print(f"No files matching 'TelemetryData*.json' found in '{data_dir}'.")
        return

    os.makedirs(graphics_dir, exist_ok=True)

    # Procesar cada archivo en orden alfabético/numérico
    for file_path in sorted(files):
        json_filename = os.path.basename(file_path)
        process_single_offset_telemetry(data_dir, graphics_dir, json_filename)


if __name__ == "__main__":
    process_offset_telemetry()