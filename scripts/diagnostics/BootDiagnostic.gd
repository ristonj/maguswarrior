extends Node

func _ready() -> void:
	print("[DIAG] GDScript _ready reached")

	# Probe 1: is cards.yaml in the APK?
	var yaml_exists := FileAccess.file_exists("res://data/cards.yaml")
	print("[DIAG] cards.yaml in APK: ", yaml_exists)

	# Probe 2: can we load the C# script resource?
	var cs_script = load("res://scripts/ui/screens/PlaceholderMainMenu.cs")
	if cs_script == null:
		print("[DIAG] FAIL: C# script resource is null")
	else:
		print("[DIAG] OK: C# script resource loaded: ", cs_script)

	# Probe 3: can we instantiate a C# script from GDScript at runtime?
	var diag_script = load("res://scripts/diagnostics/CSharpBootDiag.cs")
	if diag_script:
		var instance = diag_script.new()
		instance.name = "RuntimeCSharpDiag"
		add_child(instance)
		print("[DIAG] C# instance created and added as child: ", instance)
	else:
		print("[DIAG] FAIL: could not load CSharpBootDiag.cs")

	# Probe 4: write gds_diag.txt
	var f := FileAccess.open("user://gds_diag.txt", FileAccess.WRITE)
	if f:
		f.store_string("GDScript alive\n")
		f.close()
		print("[DIAG] OK: wrote user://gds_diag.txt")
