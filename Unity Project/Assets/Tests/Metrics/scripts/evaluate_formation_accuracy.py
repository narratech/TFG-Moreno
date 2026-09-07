import json
import math
import os
import glob

def load_json_data(file_path):
    if not os.path.exists(file_path):
        print(f"Error: File not found at path: {file_path}")
        return None
    with open(file_path, 'r', encoding='utf-8') as f:
        return json.load(f)

def vector_distance(v1, v2):
    return math.sqrt((v1['x'] - v2['x'])**2 + (v1['y'] - v2['y'])**2 + (v1['z'] - v2['z'])**2)

def evaluate_single_file(data_dir, json_filename, position_padding=1.5, offset_threshold=0.95):
    json_path = os.path.join(data_dir, json_filename)
    
    # Extrae el sufijo X (ej. FormationData1.json -> "1", FormationData.json -> "")
    suffix = json_filename[len("FormationData"):-5]

    data = load_json_data(json_path)
    if not data or 'agents' not in data:
        print(f"JSON file '{json_filename}' is empty or invalid format.")
        return

    agents = data['agents']
    total_agents = len(agents)

    if total_agents == 0:
        print(f"No agents registered in '{json_filename}'.")
        return

    arrived_count = 0
    well_positioned_count = 0
    both_criteria_count = 0

    print(f"\n==================================================")
    print(f"--- ACCURACY EVALUATION ({json_filename}) ---")
    print(f"Total Agents: {total_agents}")
    print(f"Allowed Arrival Padding: {position_padding} units")
    print(f"Optimal Offset Threshold: {offset_threshold * 100}%\n")

    for i, agent in enumerate(agents):
        name = agent.get('agentName', f'Agent_{i}')
        sample_pos = agent['samplePosition']
        target_pos = agent['targetNodePosition']
        offset_pct = agent['offsetPercentage']

        # Distancia entre la posición de muestreo con restricción y el nodo destino
        dist = vector_distance(sample_pos, target_pos)

        has_arrived = dist <= position_padding
        is_well_positioned = offset_pct >= offset_threshold

        if has_arrived:
            arrived_count += 1
        if is_well_positioned:
            well_positioned_count += 1
        if has_arrived and is_well_positioned:
            both_criteria_count += 1

        status_arrival = "ARRIVED" if has_arrived else "OUT OF RANGE"
        status_slot = "OPTIMAL SLOT" if is_well_positioned else "ADJUSTING"

        print(f"• {name} | Target-Sample Distance: {dist:.3f} ({status_arrival}) | Offset: {offset_pct*100:.1f}% ({status_slot})")

    # Estadísticas globales
    arrival_accuracy = (arrived_count / total_agents) * 100
    slot_accuracy = (well_positioned_count / total_agents) * 100
    overall_accuracy = (both_criteria_count / total_agents) * 100

    print("\n" + "="*50)
    print(f"RESULTS SUMMARY ({json_filename}):")
    print(f"Total Agents Evaluated: {total_agents}")
    print(f"Destination Arrival Rate: {arrival_accuracy:.2f}% ({arrived_count}/{total_agents})")
    print(f"Slot Placement Rate (>= {offset_threshold*100}%): {slot_accuracy:.2f}% ({well_positioned_count}/{total_agents})")
    print(f"Overall Success Rate (Both criteria): {overall_accuracy:.2f}% ({both_criteria_count}/{total_agents})")
    print("="*50)

def evaluate_agents(position_padding=1.5, offset_threshold=0.95):
    # Obtener la ruta absoluta del directorio del script
    script_dir = os.path.dirname(os.path.abspath(__file__))
    data_dir = os.path.abspath(os.path.join(script_dir, "..", "data"))

    if not os.path.exists(data_dir):
        print(f"Error: Data directory not found at '{data_dir}'")
        return

    # Buscar todos los archivos que coincidan con FormationData*.json
    search_pattern = os.path.join(data_dir, "FormationData*.json")
    files = glob.glob(search_pattern)
    
    if not files:
        print(f"No files matching 'FormationData*.json' found in '{data_dir}'.")
        return

    # Procesar cada archivo en orden numérico/alfabético
    for file_path in sorted(files):
        json_filename = os.path.basename(file_path)
        evaluate_single_file(data_dir, json_filename, position_padding, offset_threshold)

if __name__ == "__main__":
    evaluate_agents(position_padding=1.5, offset_threshold=0.95)