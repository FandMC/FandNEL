import react from "@vitejs/plugin-react";
import JavaScriptObfuscator from "javascript-obfuscator";
import { defineConfig, type Plugin } from "vite";

function obfuscateProductionBuild(): Plugin {
  return {
    name: "codexus:obfuscate-production-build",
    apply: "build",
    enforce: "post",
    renderChunk(code) {
      const result = JavaScriptObfuscator.obfuscate(code, {
        compact: true,
        controlFlowFlattening: false,
        deadCodeInjection: false,
        debugProtection: false,
        disableConsoleOutput: false,
        identifierNamesGenerator: "hexadecimal",
        log: false,
        numbersToExpressions: false,
        renameGlobals: false,
        renameProperties: false,
        selfDefending: false,
        simplify: true,
        sourceMap: false,
        splitStrings: false,
        stringArray: true,
        stringArrayCallsTransform: false,
        stringArrayEncoding: ["base64"],
        stringArrayRotate: true,
        stringArrayShuffle: true,
        stringArrayThreshold: 0.75,
        transformObjectKeys: false,
        unicodeEscapeSequence: false,
      });

      return { code: result.getObfuscatedCode(), map: null };
    },
  };
}

export default defineConfig({
  plugins: [react(), obfuscateProductionBuild()],
  publicDir: "public-app",
  server: {
    host: "127.0.0.1",
    port: 3550,
    watch: {
      interval: 250,
      usePolling: true,
    },
  },
  preview: {
    host: "127.0.0.1",
  },
});
