using Godot;

public enum FaceType { Flat = 0, QuadExt = 1, QuadInt = -1, CircExt = 2, CircInt = -2, CruzExt = 3, CruzInt = -3 }

public partial class PuzzlePiece : RigidBody3D
{
	public Vector3I TargetGridPosition;
	public FaceType[] Faces = new FaceType[6];
	public bool IsAttachedToCore = false;
	public bool IsSelected = false; 

	// O novo visual de seleção
	private MeshInstance3D highlightBox;

	public override void _Ready()
	{
		AddToGroup("PuzzlePiece");

		// Cria a caixa amarela de seleção (levemente maior que a peça)
		highlightBox = new MeshInstance3D();
		BoxMesh box = new BoxMesh();
		box.Size = new Vector3(2.1f, 2.1f, 2.1f);
		highlightBox.Mesh = box;

		StandardMaterial3D mat = new StandardMaterial3D();
		mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		mat.AlbedoColor = new Color(1.0f, 1.0f, 0.0f, 0.3f); // Amarelo translúcido
		mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded; // Brilha no escuro
		highlightBox.MaterialOverride = mat;

		AddChild(highlightBox);
		highlightBox.Visible = false;
	}

	public FaceType GetFaceInGlobalDirection(Vector3 globalDir)
	{
		Vector3 localDir = GlobalTransform.Basis.Inverse() * globalDir;
		localDir = new Vector3(Mathf.Round(localDir.X), Mathf.Round(localDir.Y), Mathf.Round(localDir.Z));

		if (localDir == Vector3.Up) return Faces[0];
		if (localDir == Vector3.Down) return Faces[1];
		if (localDir == Vector3.Right) return Faces[2];
		if (localDir == Vector3.Left) return Faces[3];
		if (localDir == Vector3.Forward) return Faces[4];
		if (localDir == Vector3.Back) return Faces[5];

		return FaceType.Flat;
	}

	public void SetSelected(bool isSelected)
	{
		IsSelected = isSelected;
		
		// Apenas liga/desliga a caixa amarela. As cores da peça ficam intactas!
		highlightBox.Visible = isSelected;

		if (isSelected) Freeze = true; 
		else if (!IsAttachedToCore) Freeze = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsSelected)
		{
			if (GlobalPosition.Y < -10.0f || Mathf.Abs(GlobalPosition.X) > 50.0f || Mathf.Abs(GlobalPosition.Z) > 50.0f)
			{
				if (!IsAttachedToCore)
				{
					LinearVelocity = Vector3.Zero;
					AngularVelocity = Vector3.Zero;
					
					RandomNumberGenerator rng = new RandomNumberGenerator();
					rng.Randomize();
					GlobalPosition = new Vector3(rng.RandfRange(-5, 5), 20.0f, rng.RandfRange(-5, 5));
				}
				else
				{
					Node parent = GetParent();
					if (parent is Node3D coreNode) coreNode.GlobalPosition = new Vector3(0, 4.0f, 0);
				}
			}
		}
	}
}
