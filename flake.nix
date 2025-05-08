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
                nodejs
                roslyn-ls
                python312
                postgresql
                (let
                    docker-compose = writeText "docker-compose.json" (lib.strings.toJSON {
                        services.postgres = {
                            image = "postgres";
                            environment = {
                                POSTGRES_USER = "root";
                                POSTGRES_DB = "autoscaler";
                                POSTGRES_PASSWORD = "password";
                            };
                            ports = [
                                "5432:5432"
                            ];
                        };
                    });
                in writeScriptBin "autoscaler" ''
                    #!${bash}/bin/bash
                    set -e
                    if test "$1" != ""; then
                        export Logging__LogLevel__Autoscaler="$1"
                    fi
                    ${docker}/bin/docker compose -f ${docker-compose} down
                    ${docker}/bin/docker compose -f ${docker-compose} up -d
                    ${dotnet-sdk_8}/bin/dotnet run --project ./Autoscaler.DbUp > /dev/null
                    ${dotnet-sdk_8}/bin/dotnet run --project ./Autoscaler.Api
                '')
            ];
        };
    };
}