#!/usr/bin/env node
// crap-report: computes the CRAP (Change Risk Anti-Pattern) score for every
// function in the frontend's Jest coverage data.
//
// CRAP(f) = complexity(f)^2 * (1 - coverage(f))^3 + complexity(f)
// (Savoia/Evans; see https://www.artima.com/weblogs/viewpost.jsp?thread=215899)
//
// This is the frontend sibling of backend/tools/CrapReport. The backend
// tool could lean on ReportGenerator for accurate per-method complexity
// from the C# source; there's no equivalent single tool for TS/TSX, so
// this combines two real outputs instead of hand-rolling a complexity
// analyzer:
//   - Jest's own coverage-final.json (Istanbul format, regardless of
//     coverageProvider) already has per-function location ranges
//     (fnMap) and per-statement hit counts (statementMap/s) -- real
//     coverage, grouped by function range.
//   - ESLint's built-in `complexity` rule, forced to report on every
//     function via a CLI override, gives accurate per-function
//     cyclomatic complexity from the actual TS/TSX AST.
//
// Correlating them is approximate, not exact, because the two tools
// don't agree on what counts as a separately-tracked "function":
// Istanbul's fnMap is grounded in what the compiled JS actually
// instruments (every fnMap entry has real, attributable coverage);
// ESLint's AST walk finds every function expression, including small
// nested closures (e.g. `startTransition(async () => {...})`) that
// often share their enclosing fnMap entry's instrumentation rather
// than getting their own. So:
//   1. Every fnMap entry becomes a row, its complexity taken from the
//      ESLint message whose line exactly matches that function's
//      declaration line (defaulting to 1 -- ESLint's complexity rule
//      only emits a message when complexity exceeds the forced
//      threshold of 1, so "no message" means "complexity is exactly
//      1" for that line, not "unknown").
//   2. Any ESLint-detected function whose line didn't match an fnMap
//      entry (almost always one of those small nested closures) still
//      gets its own row, but its coverage is inherited from the
//      smallest fnMap range that contains it -- a real but
//      approximate number, since it covers the closure plus whatever
//      else shares that range.
//
// Usage: node frontend/tools/crap-report/index.mjs <coverageFinalJsonPath> <outputMarkdownPath>

import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, relative, resolve, extname } from "node:path";

const TOP_N = 15;

function main() {
  const [coveragePath, outputPath] = process.argv.slice(2);
  if (!coveragePath || !outputPath) {
    console.error("Usage: crap-report <coverageFinalJsonPath> <outputMarkdownPath>");
    process.exit(1);
  }

  const coverage = JSON.parse(readFileSync(coveragePath, "utf8"));
  const files = Object.keys(coverage).filter((f) => [".ts", ".tsx"].includes(extname(f)));
  if (files.length === 0) {
    console.error(`No instrumented .ts/.tsx files found in '${coveragePath}'.`);
    process.exit(1);
  }

  const complexityByFile = runEslintComplexity(files);

  const rows = files.flatMap((file) => rowsForFile(file, coverage[file], complexityByFile.get(file) ?? []));
  rows.sort((a, b) => b.crap - a.crap);

  const topOutputPath = outputPath.replace(/\.md$/, `-top${TOP_N}.md`);
  writeMarkdownReport(outputPath, rows, "# CRAP Score Report (frontend)");
  writeMarkdownReport(topOutputPath, rows.slice(0, TOP_N), `## CRAP Score Report (top ${TOP_N}, frontend)`);
  printTopOffenders(rows, TOP_N);

  console.log();
  console.log(`Full report (${rows.length} functions): ${outputPath}`);
  console.log(`Top ${TOP_N} snippet: ${topOutputPath}`);
}

// Runs ESLint's complexity rule (forced to report on every function, not
// just violations) across all instrumented files in one invocation.
// Returns file -> [{ line, endLine, complexity, descriptor }].
//
// ESLint exits non-zero whenever it finds any "error" -- which the
// --rule override guarantees for every function with complexity above
// 1 -- so a non-zero exit here is the expected, successful case, not a
// failure; only a missing/unparseable stdout means something actually
// went wrong.
function runEslintComplexity(files) {
  const relativeFiles = files.map((f) => relative(process.cwd(), f));
  let json;
  try {
    json = execFileSync(
      "npx",
      ["eslint", "--rule", '{"complexity":["error",1]}', "--format", "json", ...relativeFiles],
      { encoding: "utf8", maxBuffer: 64 * 1024 * 1024, cwd: process.cwd() },
    );
  } catch (err) {
    if (!err.stdout) throw err;
    json = err.stdout;
  }
  return parseEslintJson(json, files);
}

function parseEslintJson(json, originalFiles) {
  const byFile = new Map();
  for (const fileResult of JSON.parse(json)) {
    const original = originalFiles.find((f) => resolve(fileResult.filePath) === resolve(f));
    if (!original) continue;
    const messages = fileResult.messages
      .filter((m) => m.ruleId === "complexity")
      .map((m) => {
        const match = /complexity of (\d+)/.exec(m.message);
        return {
          line: m.line,
          endLine: m.endLine ?? m.line,
          complexity: match ? Number(match[1]) : 1,
          descriptor: m.message.replace(/ has a complexity.*$/, ""),
        };
      });
    byFile.set(original, messages);
  }
  return byFile;
}

function rowsForFile(file, fileCoverage, eslintMessages) {
  const label = relative(process.cwd(), file);
  const fnEntries = Object.values(fileCoverage.fnMap);
  const rows = [];
  const matchedEslintLines = new Set();

  for (const [fnId, fn] of Object.entries(fileCoverage.fnMap)) {
    const coveragePercent = functionCoverage(fileCoverage, fn);
    const exactMatch = eslintMessages.find((m) => m.line === fn.decl.start.line);
    const complexity = exactMatch ? exactMatch.complexity : 1;
    if (exactMatch) matchedEslintLines.add(exactMatch.line);
    rows.push(buildRow(label, fn.name || `(anonymous at line ${fn.decl.start.line})`, complexity, coveragePercent));
  }

  for (const m of eslintMessages) {
    if (matchedEslintLines.has(m.line)) continue;
    const enclosing = fnEntries.find((fn) => fn.loc.start.line <= m.line && m.line <= fn.loc.end.line);
    const coveragePercent = enclosing ? functionCoverage(fileCoverage, enclosing) : fileLineCoverage(fileCoverage);
    rows.push(buildRow(label, `${m.descriptor} (line ${m.line})`, m.complexity, coveragePercent));
  }

  return rows;
}

function buildRow(classLabel, methodName, complexity, coveragePercent) {
  return { classLabel, methodName, complexity, coveragePercent, crap: crap(complexity, coveragePercent) };
}

function crap(complexity, coveragePercent) {
  const uncovered = 1 - coveragePercent / 100;
  return complexity * complexity * uncovered ** 3 + complexity;
}

// Attributes each statement to the smallest fnMap range containing it,
// then returns the percentage of that function's own statements with a
// nonzero hit count. Falls back to the function's own call count (100%
// if ever called, else 0%) when it has no attributable statements.
function functionCoverage(fileCoverage, targetFn) {
  const fnEntries = Object.entries(fileCoverage.fnMap);
  let covered = 0;
  let total = 0;
  for (const [stmtId, stmt] of Object.entries(fileCoverage.statementMap)) {
    const smallest = smallestEnclosingFn(fnEntries, stmt);
    if (smallest !== targetFn) continue;
    total += 1;
    if (fileCoverage.s[stmtId] > 0) covered += 1;
  }
  if (total === 0) {
    const fnId = fnEntries.find(([, fn]) => fn === targetFn)?.[0];
    return fnId !== undefined && fileCoverage.f[fnId] > 0 ? 100 : 0;
  }
  return (covered / total) * 100;
}

function smallestEnclosingFn(fnEntries, stmt) {
  let best = null;
  let bestSpan = Infinity;
  for (const [, fn] of fnEntries) {
    if (fn.loc.start.line > stmt.start.line || fn.loc.end.line < stmt.end.line) continue;
    const span = fn.loc.end.line - fn.loc.start.line;
    if (span < bestSpan) {
      best = fn;
      bestSpan = span;
    }
  }
  return best;
}

function fileLineCoverage(fileCoverage) {
  const hits = Object.values(fileCoverage.s);
  if (hits.length === 0) return 100;
  return (hits.filter((h) => h > 0).length / hits.length) * 100;
}

function printTopOffenders(rows, take) {
  console.log(`Top ${Math.min(take, rows.length)} by CRAP score:`);
  console.log(`${"CRAP".padStart(8)}  ${"Complexity".padStart(10)}  ${"Coverage".padStart(8)}  File / Function`);
  for (const r of rows.slice(0, take)) {
    console.log(
      `${r.crap.toFixed(1).padStart(8)}  ${String(r.complexity).padStart(10)}  ${`${Math.round(r.coveragePercent)}%`.padStart(8)}  ${r.classLabel} :: ${r.methodName}`,
    );
  }
}

function writeMarkdownReport(outputPath, rows, heading) {
  const lines = [
    heading,
    "",
    "CRAP(f) = complexity(f)^2 * (1 - coverage(f))^3 + complexity(f)",
    "",
    "A low score means low complexity, high coverage, or both -- low change",
    "risk. A high score means a complex function with low test coverage --",
    "the combination this metric exists to flag. This is a report, not a",
    "CI gate: nothing here fails a build.",
    "",
    "| CRAP | Complexity | Coverage | File | Function |",
    "|-----:|-----------:|---------:|------|----------|",
    ...rows.map(
      (r) => `| ${r.crap.toFixed(1)} | ${r.complexity} | ${Math.round(r.coveragePercent)}% | ${r.classLabel} | ${r.methodName} |`,
    ),
  ];
  mkdirSync(dirname(resolve(outputPath)), { recursive: true });
  writeFileSync(outputPath, lines.join("\n") + "\n");
}

main();
