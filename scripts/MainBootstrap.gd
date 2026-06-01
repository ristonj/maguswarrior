extends Node

func _ready() -> void:
	print("[BOOT] MainBootstrap._ready entered")
	var script = load("res://scripts/ui/screens/PlaceholderMainMenu.cs")
	if script == null:
		print("[BOOT] ERROR: failed to load PlaceholderMainMenu.cs")
		return
	print("[BOOT] C# script loaded OK: ", script)
	var menu = script.new()
	if menu == null:
		print("[BOOT] ERROR: script.new() returned null")
		return
	print("[BOOT] menu instantiated OK: ", menu)
	menu.name = "PlaceholderMainMenu"
	get_tree().get_root().add_child(menu)
	print("[BOOT] menu added to scene tree")
