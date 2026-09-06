import json
import math
import os

def load_json_data(file_path):
    if not os.path.exists(file_path):
        print(f"Error: No se encontró el archivo en la ruta: {file_path}")
        return None
    with open(file_path, 'r', encoding='utf-8') as f:
        return json.load(f)

def vector_distance(v1, v2):
    return math.sqrt((v1['x'] - v2['x'])**2 + (v1['y'] - v2['y'])**2 + (v1['z'] - v2['z'])**2)

def evaluate_agents(json_file_path, position_padding=1.5, offset_threshold=0.95):
    data = load_json_data(json_file_path)
    if not data or 'agents' not in data:
        print("El archivo JSON está vacío o no tiene el formato correcto.")
        return

    agents = data['agents']
    total_agents = len(agents)

    if total_agents == 0:
        print("No hay agentes registrados en el JSON.")
        return

    arrived_count = 0
    well_positioned_count = 0
    both_criteria_count = 0

    print(f"--- EVALUACIÓN DE ACCURACY ({total_agents} agentes) ---")
    print(f"Padding de llegada admitido: {position_padding} unidades")
    print(f"Umbral de Offset óptimo: {offset_threshold * 100}%\n")

    for i, agent in enumerate(agents):
        name = agent.get('agentName', f'Agent_{i}')
        sample_pos = agent['samplePosition']
        target_pos = agent['targetNodePosition']
        offset_pct = agent['offsetPercentage']

        # Distancia entre la posición de sampleo con constraint y el nodo target
        dist = vector_distance(sample_pos, target_pos)

        has_arrived = dist <= position_padding
        is_well_positioned = offset_pct >= offset_threshold

        if has_arrived:
            arrived_count += 1
        if is_well_positioned:
            well_positioned_count += 1
        if has_arrived and is_well_positioned:
            both_criteria_count += 1

        status_arrival = "LLEGÓ" if has_arrived else "FUERA DE RANGO"
        status_slot = "SLOT ÓPTIMO" if is_well_positioned else "AJUSTÁNDOSE"

        print(f"• {name} | Distancia Target-Sample: {dist:.3f} ({status_arrival}) | Offset: {offset_pct*100:.1f}% ({status_slot})")

    # Estadísticas globales
    arrival_accuracy = (arrived_count / total_agents) * 100
    slot_accuracy = (well_positioned_count / total_agents) * 100
    overall_accuracy = (both_criteria_count / total_agents) * 100

    print("\n" + "="*50)
    print("RESUMEN DE RESULTADOS:")
    print(f"Total Agentes Evaluados: {total_agents}")
    print(f"Porcentaje de Llegada al Destino: {arrival_accuracy:.2f}% ({arrived_count}/{total_agents})")
    print(f"Porcentaje con Slot Colocado (>= {offset_threshold*100}%): {slot_accuracy:.2f}% ({well_positioned_count}/{total_agents})")
    print(f"Porcentaje de Éxito Total (Ambos criterios): {overall_accuracy:.2f}% ({both_criteria_count}/{total_agents})")
    print("="*50)

if __name__ == "__main__":
    # Nota: Copia el archivo 'formation_data.json' desde el persistentDataPath de Unity 
    # a la misma carpeta de este script, o ajusta la ruta completa aquí.
    file_path = "formation_data.json"
    evaluate_agents(file_path, position_padding=1.5, offset_threshold=0.95)