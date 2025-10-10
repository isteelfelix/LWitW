import json
import os
from collections import defaultdict

def extract_strings(obj, strings):
    if isinstance(obj, dict):
        if "title" in obj and "value" in obj and isinstance(obj["value"], str):
            title = obj["title"]
            if "en" in title and not any(lang in title for lang in ["ko", "zh", "ja"]):
                strings.add(obj["value"])
        else:
            for value in obj.values():
                extract_strings(value, strings)
    elif isinstance(obj, list):
        for item in obj:
            extract_strings(item, strings)

def main():
    res_dir = 'res'
    if not os.path.exists(res_dir):
        print("res folder not found")
        return

    all_strings = set()

    for filename in os.listdir(res_dir):
        if filename.endswith('.json'):
            filepath = os.path.join(res_dir, filename)
            try:
                with open(filepath, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                    extract_strings(data, all_strings)
            except Exception as e:
                print(f"Error parsing {filename}: {e}")

    # Filter strings: remove empty, very short, or non-text
    filtered_strings = {s for s in all_strings if s and len(s) > 1 and not s.isdigit()}

    # Read existing ru.json if it exists
    existing_translations = {}
    ru_file = 'ru.json'
    if os.path.exists(ru_file):
        try:
            with open(ru_file, 'r', encoding='utf-8') as f:
                existing_translations = json.load(f)
        except Exception as e:
            print(f"Error reading existing {ru_file}: {e}")

    # Add new strings that are not already in existing translations
    new_count = 0
    for text in filtered_strings:
        if text not in existing_translations:
            existing_translations[text] = ""
            new_count += 1

    # Write updated ru.json
    with open(ru_file, 'w', encoding='utf-8') as f:
        json.dump(existing_translations, f, ensure_ascii=False, indent=4, sort_keys=True)

    print(f"Updated ru.json: added {new_count} new entries, total {len(existing_translations)} entries")

if __name__ == "__main__":
    main()
