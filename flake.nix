{
  description = "Autoscaler nix flake";

  inputs = {
    nixpkgs.url = "nixpkgs/nixos-unstable";
  };

  outputs = { self, nixpkgs }: let
    system = "x86_64-linux";
    pkgs = import nixpkgs {inherit system;};
  in {
    devShells.${system}.default = pkgs.mkShellNoCC {
      packages = with pkgs; [
        dotnetCorePackages.dotnet_8.sdk
        dotnetCorePackages.dotnet_8.aspnetcore
        dotnetCorePackages.dotnet_8.runtime
        roslyn-ls
        python312
      ];
    };
  };
}
