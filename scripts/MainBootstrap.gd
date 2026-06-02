extends Node

# Entry point. The main scene's root node runs this on launch and mounts the
# placeholder UI. PlaceholderMainMenu is a C# CanvasLayer that builds its own
# children in _Ready(), so we just instantiate it and add it under this node.
func _ready() -> void:
	var menu_script := load("res://scripts/ui/screens/PlaceholderMainMenu.cs")
	if menu_script == null:
		push_error("MainBootstrap: failed to load PlaceholderMainMenu.cs")
		return
	var menu = menu_script.new()
	if menu == null:
		push_error("MainBootstrap: PlaceholderMainMenu.new() returned null")
		return
	menu.name = "PlaceholderMainMenu"
	# Add to self, not get_tree().get_root(): during _ready the Window root is
	# still busy adding the main scene, so add_child() on it fails.
	add_child(menu)
