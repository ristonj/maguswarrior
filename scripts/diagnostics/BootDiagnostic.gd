extends Node

func _ready() -> void:
	print("[DIAG] GDScript _ready reached")

	# Probe 1: is cards.yaml in the APK?
	var yaml_exists := FileAccess.file_exists("res://data/cards.yaml")
	print("[DIAG] cards.yaml in APK: ", yaml_exists)

	# Probe 2: can we load the C# script resource at all?
	var cs_script = load("res://scripts/ui/screens/PlaceholderMainMenu.cs")
	if cs_script == null:
		print("[DIAG] FAIL: C# script resource is null — assembly not loaded or path mismatch")
	else:
		print("[DIAG] OK: C# script resource loaded: ", cs_script)

	# Probe 3: write a file so we can confirm GDScript user:// I/O works
	var f := FileAccess.open("user://gds_diag.txt", FileAccess.WRITE)
	if f == null:
		print("[DIAG] FAIL: could not open user://gds_diag.txt (error=%d)" % FileAccess.get_open_error())
	else:
		f.store_string("GDScript alive\n")
		f.close()
		print("[DIAG] OK: wrote user://gds_diag.txt")
