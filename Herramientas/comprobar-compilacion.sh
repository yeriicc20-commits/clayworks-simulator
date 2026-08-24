#!/usr/bin/env bash
#
# Compila todos los scripts del proyecto SIN abrir Unity.
#
# Sirve para cazar errores de compilacion antes de darle a Play, que en un
# proyecto de este tamano tarda bastante mas en decirtelo. Usa el Roslyn y las
# DLL que ya trae instalado el propio Unity, asi que no hace falta instalar nada.
#
#   bash Herramientas/comprobar-compilacion.sh
#
# Si no imprime ninguna linea "error CS", compila.
set -u

UNITY_VER="${UNITY_VER:-6000.5.8f1}"
U="/c/Program Files/Unity/Hub/Editor/$UNITY_VER/Editor/Data"

if [ ! -d "$U" ]; then
    echo "No encuentro Unity $UNITY_VER en $U"
    echo "Si tienes otra version: UNITY_VER=6000.x.yfz bash $0"
    exit 1
fi

CSC=$(ls "$U/DotNetSdk/sdk/"*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)

if [ -z "$CSC" ]; then
    echo "No encuentro el compilador dentro de Unity."
    exit 1
fi

RAIZ="$(cd "$(dirname "$0")/.." && pwd)"
RSP="$(mktemp)"

# OJO: solo los modulos UnityEditor.*Module.dll, nunca ademas UnityEditor.dll.
# Los tipos estan en los dos y salen cientos de CS0433 que no son del proyecto.
{
    echo "-r:\"$(cygpath -w "$U/NetStandard/ref/2.1.0/netstandard.dll")\""

    for d in "$U/Managed/UnityEngine/"*.dll; do
        echo "-r:\"$(cygpath -w "$d")\""
    done

    # Los paquetes ya compilados: TextMeshPro, navegacion, URP...
    for d in "$RAIZ"/Library/ScriptAssemblies/*.dll; do
        case "$d" in *Assembly-CSharp*) continue;; esac
        [ -e "$d" ] || continue
        echo "-r:\"$(cygpath -w "$d")\""
    done

    find "$RAIZ/Assets" -name "*.cs" -not -name "*.bak" | while read -r f; do
        echo "\"$(cygpath -w "$f")\""
    done
} > "$RSP"

SALIDA="$(mktemp -u).dll"

"$U/DotNetSdk/dotnet.exe" "$(cygpath -w "$CSC")" \
    -target:library -nostdlib -noconfig -langversion:9.0 \
    -out:"$(cygpath -w "$SALIDA")" "@$(cygpath -w "$RSP")" 2>&1 | grep -E "error CS"

if [ -e "$SALIDA" ]; then
    echo "COMPILA LIMPIO"
    rm -f "$SALIDA"
    CODIGO=0
else
    echo "NO COMPILA"
    CODIGO=1
fi

rm -f "$RSP"
exit $CODIGO
