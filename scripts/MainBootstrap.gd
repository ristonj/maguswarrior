extends Node

func _ready() -> void:
	var script = load("res://scripts/ui/screens/PlaceholderMainMenu.cs")
	var menu = script.new()
	menu.name = "PlaceholderMainMenu"
	get_tree().get_root().add_child(menu)
