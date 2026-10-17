using Godot;
using System.Collections.Generic;

public partial class OrbitCamera
{
	private Control uiContainer;
	
	// UI - Mapa Principal (Topologia Atual)
	private ColorRect mapBackground;
	private Control mapCanvas;
	private Label helpLabel;

	// UI - Mapa de Previsão (Fundido com o Grupo)
	private ColorRect previewBackground;
	private Control previewCanvas;
	private Label previewLabel;
	
	private Texture2D texFlat, texQuadExt, texQuadInt, texCircExt, texCircInt, texCruzExt, texCruzInt;

	private List<TextureRect> mainIconPool = new List<TextureRect>();
	private int activeMainIcons = 0;

	private List<TextureRect> previewIconPool = new List<TextureRect>();
	private int activePreviewIcons = 0;

	private Vector2 mapPan = Vector2.Zero;
	private float mapZoom = 0.8f;
	private bool isDraggingMap = false;

	private void LoadTextures()
	{
		// CORREÇÃO: Carregando as imagens 2D estritas para a UI
		texFlat = GD.Load<Texture2D>("res://UI/Icons/Flat.png");
		texQuadExt = GD.Load<Texture2D>("res://UI/Icons/QuadExt.png");
		texQuadInt = GD.Load<Texture2D>("res://UI/Icons/QuadInt.png");
		texCircExt = GD.Load<Texture2D>("res://UI/Icons/CircExt.png");
		texCircInt = GD.Load<Texture2D>("res://UI/Icons/CircInt.png");
		texCruzExt = GD.Load<Texture2D>("res://UI/Icons/CruzExt.png");
		texCruzInt = GD.Load<Texture2D>("res://UI/Icons/CruzInt.png");
	}

	private void Build2DMinimapUI()
	{
		CanvasLayer canvas = new CanvasLayer();
		AddChild(canvas);

		uiContainer = new Control();
		uiContainer.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		canvas.AddChild(uiContainer);

		// MAPA PRINCIPAL (Em Cima)
		mapBackground = new ColorRect();
		mapBackground.Color = new Color(0.02f, 0.05f, 0.1f, 0.95f);
		mapBackground.Size = new Vector2(360, 260);
		mapBackground.Position = new Vector2(20, 20);
		mapBackground.ClipContents = true; 
		mapBackground.GuiInput += OnMapGuiInput;
		uiContainer.AddChild(mapBackground);

		mapCanvas = new Control();
		mapCanvas.Size = new Vector2(360, 260);
		mapBackground.AddChild(mapCanvas);

		helpLabel = new Label();
		helpLabel.Position = new Vector2(30, 30);
		helpLabel.LabelSettings = new LabelSettings() { FontSize = 11, FontColor = new Color(0.4f, 0.8f, 1.0f) };
		uiContainer.AddChild(helpLabel);

		// MAPA DE PREVISÃO (Em Baixo)
		previewBackground = new ColorRect();
		previewBackground.Color = new Color(0.1f, 0.05f, 0.15f, 0.95f);
		previewBackground.Size = new Vector2(360, 260);
		previewBackground.Position = new Vector2(20, 300); 
		previewBackground.ClipContents = true; 
		previewBackground.GuiInput += OnMapGuiInput; 
		uiContainer.AddChild(previewBackground);

		previewCanvas = new Control();
		previewCanvas.Size = new Vector2(360, 260);
		previewBackground.AddChild(previewCanvas);

		previewLabel = new Label();
		previewLabel.Position = new Vector2(30, 310);
		previewLabel.LabelSettings = new LabelSettings() { FontSize = 12 };
		uiContainer.AddChild(previewLabel);

		ApplyMapTransform();
	}

	private void OnMapGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Middle || mb.ButtonIndex == MouseButton.Left)
				isDraggingMap = mb.Pressed;
			
			if (mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
			{
				float oldZoom = mapZoom;
				if (mb.ButtonIndex == MouseButton.WheelUp) mapZoom = Mathf.Min(mapZoom * 1.2f, 4.0f);
				if (mb.ButtonIndex == MouseButton.WheelDown) mapZoom = Mathf.Max(mapZoom / 1.2f, 0.2f);
				
				Vector2 mousePos = mapBackground.GetLocalMousePosition();
				Vector2 localPos = (mousePos - mapPan) / oldZoom;
				mapPan = mousePos - localPos * mapZoom;
				
				ApplyMapTransform();
			}
		}
		else if (@event is InputEventMouseMotion motion && isDraggingMap)
		{
			mapPan += motion.Relative;
			ApplyMapTransform();
		}
	}

	private void ApplyMapTransform()
	{
		if (mapCanvas != null) { mapCanvas.Scale = new Vector2(mapZoom, mapZoom); mapCanvas.Position = mapPan; }
		if (previewCanvas != null) { previewCanvas.Scale = new Vector2(mapZoom, mapZoom); previewCanvas.Position = mapPan; }
	}

	private TextureRect GetIconFromPool(List<TextureRect> pool, ref int activeCount, Control canvas)
	{
		if (activeCount < pool.Count)
		{
			TextureRect icon = pool[activeCount];
			icon.Visible = true;
			activeCount++;
			return icon;
		}

		TextureRect newIcon = new TextureRect();
		newIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		
		ColorRect bg = new ColorRect();
		bg.Color = new Color(0.1f, 0.3f, 0.5f, 0.8f);
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.ShowBehindParent = true;
		newIcon.AddChild(bg);

		canvas.AddChild(newIcon);
		pool.Add(newIcon);
		activeCount++;
		return newIcon;
	}

	private void HideUnusedIcons()
	{
		for (int i = activeMainIcons; i < mainIconPool.Count; i++) mainIconPool[i].Visible = false;
		for (int i = activePreviewIcons; i < previewIconPool.Count; i++) previewIconPool[i].Visible = false;
	}

	private void UpdateUIProcess()
	{
		activeMainIcons = 0; 
		activePreviewIcons = 0;

		if (selectedPiece == null && !isHoldingCore)
		{
			helpLabel.Text = "ESTADO: A aguardar seleção...\n(Clique Esq: Selecionar Peça)";
			previewBackground.Visible = false;
			previewLabel.Visible = false;
			HideUnusedIcons();
			return;
		}

		string joinKey = InputMap.HasAction("join_piece") ? "[join_piece]" : "Ação não configurada";

		helpLabel.Text = isHoldingCore 
			? "TOPOLOGIA ATUAL (GRUPO)\n(Clique Esq: Largar)" 
			: "TOPOLOGIA ATUAL (PEÇA)\n(Rotacionar: Setas)";

		// 1. DESENHA A TOPOLOGIA ATUAL
		List<(Vector3 pos, PuzzlePiece piece)> currentPieces = new List<(Vector3, PuzzlePiece)>();
		if (isHoldingCore)
		{
			foreach (Node n in coreGroup.GetChildren())
				if (n is PuzzlePiece p) currentPieces.Add((p.GlobalPosition, p));
		}
		else
		{
			currentPieces.Add((selectedPiece.GlobalPosition, selectedPiece));
		}

		DrawLayoutOnCanvas(mapCanvas, currentPieces, mainIconPool, ref activeMainIcons);

		// 2. DESENHA A TOPOLOGIA DE PREVISÃO (Aparece se houver o fantasma de união)
		if (!isHoldingCore && snapGhost.Visible)
		{
			previewBackground.Visible = true;
			previewLabel.Visible = true;
			
			if (isCurrentlyValidSnap)
			{
				previewLabel.Text = $"PREVISÃO: ENCAIXE VÁLIDO!\nPressione {joinKey} para confirmar a união.";
				previewLabel.LabelSettings.FontColor = Colors.Green;
			}
			else
			{
				previewLabel.Text = $"PREVISÃO: COLISÃO / INCOMPATÍVEL\nAs faces não se encaixam.";
				previewLabel.LabelSettings.FontColor = Colors.Red;
			}

			List<(Vector3 pos, PuzzlePiece piece)> previewPieces = new List<(Vector3, PuzzlePiece)>();
			foreach (Node n in coreGroup.GetChildren())
			{
				if (n is PuzzlePiece p) previewPieces.Add((p.GlobalPosition, p));
			}
			// Simula a posição da peça na localização do fantasma para mostrar como a nova topologia ficará
			previewPieces.Add((currentSnapWorldPos, selectedPiece));

			DrawLayoutOnCanvas(previewCanvas, previewPieces, previewIconPool, ref activePreviewIcons);
		}
		else
		{
			previewBackground.Visible = false;
			previewLabel.Visible = false;
		}

		HideUnusedIcons();
	}

	private void DrawLayoutOnCanvas(Control canvas, List<(Vector3 pos, PuzzlePiece piece)> pieces, List<TextureRect> pool, ref int activeCount)
	{
		float cx = 180f; 
		float cy = 130f;
		float spacing = 80f; 
		float iconSize = 22f; 

		DrawProjectedView(canvas, pool, ref activeCount, Vector3.Forward, Vector3.Right, Vector3.Up, new Vector2(cx, cy), pieces, iconSize);
		DrawProjectedView(canvas, pool, ref activeCount, Vector3.Back, Vector3.Left, Vector3.Up, new Vector2(cx + spacing * 3, cy), pieces, iconSize);
		DrawProjectedView(canvas, pool, ref activeCount, Vector3.Right, Vector3.Back, Vector3.Up, new Vector2(cx + spacing, cy), pieces, iconSize);
		DrawProjectedView(canvas, pool, ref activeCount, Vector3.Left, Vector3.Forward, Vector3.Up, new Vector2(cx - spacing, cy), pieces, iconSize);
		DrawProjectedView(canvas, pool, ref activeCount, Vector3.Up, Vector3.Right, Vector3.Back, new Vector2(cx, cy - spacing), pieces, iconSize);
		DrawProjectedView(canvas, pool, ref activeCount, Vector3.Down, Vector3.Right, Vector3.Forward, new Vector2(cx, cy + spacing), pieces, iconSize);
	}

	private void DrawProjectedView(Control canvas, List<TextureRect> pool, ref int activeCount, Vector3 viewDir, Vector3 layoutRight, Vector3 layoutUp, Vector2 centerScreenPos, List<(Vector3 pos, PuzzlePiece piece)> pieces, float iconSize)
	{
		Dictionary<Vector2I, (float depth, FaceType face)> projection = new Dictionary<Vector2I, (float, FaceType)>();

		foreach (var item in pieces)
		{
			Vector3 pos = item.pos;
			int x = Mathf.RoundToInt(pos.Dot(layoutRight) / 2f);
			int y = Mathf.RoundToInt(pos.Dot(layoutUp) / 2f);
			float depth = pos.Dot(viewDir); 

			Vector2I coord = new Vector2I(x, y);
			
			if (!projection.ContainsKey(coord) || projection[coord].depth < depth)
			{
				FaceType face = item.piece.GetFaceInGlobalDirection(viewDir);
				projection[coord] = (depth, face);
			}
		}

		float minX = float.MaxValue, maxX = float.MinValue;
		float minY = float.MaxValue, maxY = float.MinValue;
		foreach (var coord in projection.Keys) { if (coord.X < minX) minX = coord.X; if (coord.X > maxX) maxX = coord.X; if (coord.Y < minY) minY = coord.Y; if (coord.Y > maxY) maxY = coord.Y; }

		float offsetX = -(maxX + minX) / 2f;
		float offsetY = -(maxY + minY) / 2f;

		foreach (var kvp in projection)
		{
			Vector2I coord = kvp.Key;
			FaceType face = kvp.Value.face;

			float screenX = centerScreenPos.X + (coord.X + offsetX) * iconSize - iconSize / 2f;
			float screenY = centerScreenPos.Y - (coord.Y + offsetY) * iconSize - iconSize / 2f;

			TextureRect icon = GetIconFromPool(pool, ref activeCount, canvas);
			icon.Position = new Vector2(screenX, screenY);
			icon.Size = new Vector2(iconSize, iconSize);
			icon.Texture = GetFaceTexture(face);
		}
	}

	private Texture2D GetFaceTexture(FaceType type)
	{
		switch (type)
		{
			case FaceType.QuadExt: return texQuadExt;
			case FaceType.QuadInt: return texQuadInt;
			case FaceType.CircExt: return texCircExt;
			case FaceType.CircInt: return texCircInt;
			case FaceType.CruzExt: return texCruzExt;
			case FaceType.CruzInt: return texCruzInt;
			default: return texFlat;
		}
	}
}
