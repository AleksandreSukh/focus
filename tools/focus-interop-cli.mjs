#!/usr/bin/env node

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import {
  TASK_STATE,
  normalizeMindMapDocument,
  parseMindMapDocument,
  serializeMindMapDocument,
} from '../pwa/src/maps/model.js';
import {
  LLM_JOB_STATUS,
  applyLlmJobCompletion,
  buildLlmContext,
  claimLlmJob,
  completeLlmJob,
  failLlmJob,
  formatLlmContextMarkdown,
  normalizeLlmJob,
  serializeLlmJob,
} from '../pwa/src/llm/interop.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

if (import.meta.url === pathToFileURL(path.resolve(process.argv[1] || '')).href) {
  runCli(process.argv.slice(2)).catch((error) => {
    console.error(error?.message || String(error));
    process.exitCode = 1;
  });
}

export async function runCli(argv, io = { stdout: process.stdout, stderr: process.stderr }) {
  const parsed = parseArgs(argv);
  const [command, subcommand] = parsed.positionals;
  const mapsDir = resolveMapsDir(parsed.options['maps-dir']);

  if (!command || parsed.options.help) {
    io.stdout.write(usage());
    return;
  }

  if (command === 'context') {
    const mapArg = requireOption(parsed.options, 'map');
    const nodeId = requireOption(parsed.options, 'node');
    const format = parsed.options.format || 'markdown';
    const workspace = loadWorkspace(mapsDir, mapArg);
    const context = buildLlmContext({
      snapshot: workspace.selectedSnapshot,
      nodeId,
      snapshots: workspace.snapshots,
    });
    if (!context) {
      throw new Error(`Node "${nodeId}" was not found in "${mapArg}".`);
    }

    writeContext(io.stdout, context, format);
    return;
  }

  if (command !== 'jobs') {
    throw new Error(`Unknown command "${command}".\n\n${usage()}`);
  }

  switch (subcommand) {
    case 'list':
      listJobs(io.stdout, mapsDir, parsed.options);
      return;
    case 'claim':
      claimJob(io.stdout, mapsDir, parsed.options);
      return;
    case 'complete':
      completeJob(io.stdout, mapsDir, parsed.options);
      return;
    case 'fail':
      failJob(io.stdout, mapsDir, parsed.options);
      return;
    default:
      throw new Error(`Unknown jobs command "${subcommand || ''}".\n\n${usage()}`);
  }
}

function listJobs(stdout, mapsDir, options) {
  const status = options.status || 'open';
  const jobs = loadJobs(mapsDir)
    .filter((entry) => status === 'all' || (
      status === 'open'
        ? [LLM_JOB_STATUS.PENDING, LLM_JOB_STATUS.CLAIMED].includes(entry.job.status)
        : entry.job.status === status
    ));

  if (options.format === 'json') {
    stdout.write(`${JSON.stringify(jobs.map((entry) => entry.job), null, 2)}\n`);
    return;
  }

  if (jobs.length === 0) {
    stdout.write('No LLM jobs found.\n');
    return;
  }

  jobs.forEach(({ job }) => {
    stdout.write(`${job.status.padEnd(9)} ${job.id} ${job.mapFilePath}#${job.nodeId} ${job.prompt}\n`);
  });
}

function claimJob(stdout, mapsDir, options) {
  const agent = requireOption(options, 'agent');
  const entry = findJob(mapsDir, options.job, [LLM_JOB_STATUS.PENDING]);
  const nextJob = claimLlmJob(entry.job, { agent });
  saveJobEntry(entry, nextJob);

  const workspace = loadWorkspace(mapsDir, nextJob.mapFilePath);
  const context = buildLlmContext({
    snapshot: workspace.selectedSnapshot,
    nodeId: nextJob.nodeId,
    snapshots: workspace.snapshots,
  });
  if (!context) {
    throw new Error(`Prompt node "${nextJob.nodeId}" was not found in "${nextJob.mapFilePath}".`);
  }

  writeContext(stdout, context, options.format || 'markdown');
}

function completeJob(stdout, mapsDir, options) {
  const jobId = requireOption(options, 'job');
  const answerFile = requireOption(options, 'answer-file');
  const entry = findJob(mapsDir, jobId, [LLM_JOB_STATUS.PENDING, LLM_JOB_STATUS.CLAIMED]);
  const agent = options.agent || entry.job.claimedBy || 'focus-interop';
  const answer = fs.readFileSync(path.resolve(answerFile), 'utf8');
  const mapPath = resolveMapPath(mapsDir, entry.job.mapFilePath);
  const document = loadMapDocument(mapPath);
  const applied = applyLlmJobCompletion(document, entry.job, {
    answer,
    agent,
  });
  if (!applied.ok) {
    throw new Error(applied.error.message);
  }

  fs.writeFileSync(mapPath, serializeMindMapDocument(normalizeMindMapDocument(document)), 'utf8');
  const nextJob = completeLlmJob(entry.job, {
    agent,
    result: {
      mapFilePath: entry.job.mapFilePath,
      promptNodeId: applied.value.promptNodeId,
      answerNodeId: applied.value.answerNodeId,
    },
  });
  saveJobEntry(entry, nextJob);
  stdout.write(`Completed ${nextJob.id}; appended ${applied.value.answerNodeId}.\n`);
}

function failJob(stdout, mapsDir, options) {
  const jobId = requireOption(options, 'job');
  const message = requireOption(options, 'message');
  const entry = findJob(mapsDir, jobId, [LLM_JOB_STATUS.PENDING, LLM_JOB_STATUS.CLAIMED]);
  const nextJob = failLlmJob(entry.job, { message });
  saveJobEntry(entry, nextJob);
  stdout.write(`Failed ${nextJob.id}: ${nextJob.errorMessage}\n`);
}

function writeContext(stdout, context, format) {
  if (format === 'json') {
    stdout.write(`${JSON.stringify(context, null, 2)}\n`);
    return;
  }

  if (format !== 'markdown') {
    throw new Error(`Unsupported format "${format}". Use json or markdown.`);
  }

  stdout.write(formatLlmContextMarkdown(context));
}

function loadWorkspace(mapsDir, selectedMapArg) {
  const selectedMapPath = resolveMapPath(mapsDir, selectedMapArg);
  const selectedSnapshot = loadSnapshot(mapsDir, selectedMapPath, selectedMapArg);
  const snapshots = listMapFiles(mapsDir)
    .map((filePath) => loadSnapshot(mapsDir, filePath, protocolPathForMap(mapsDir, filePath)))
    .filter((snapshot) => snapshot.filePath !== selectedSnapshot.filePath);

  return {
    selectedSnapshot,
    snapshots: [selectedSnapshot, ...snapshots],
  };
}

function loadSnapshot(mapsDir, filePath, protocolPath) {
  const document = loadMapDocument(filePath);
  const fileName = path.basename(filePath);
  return {
    filePath: normalizeProtocolPath(protocolPath || protocolPathForMap(mapsDir, filePath)),
    fileName,
    mapName: fileName.replace(/\.json$/i, ''),
    document,
    revision: '',
    loadedAt: Date.now(),
  };
}

function loadMapDocument(filePath) {
  const raw = fs.readFileSync(filePath, 'utf8');
  return normalizeMindMapDocument(parseMindMapDocument(raw), {
    fileTimestampIso: fs.statSync(filePath).mtime.toISOString(),
  });
}

function listMapFiles(mapsDir) {
  if (!fs.existsSync(mapsDir)) {
    return [];
  }

  return fs.readdirSync(mapsDir, { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.toLowerCase().endsWith('.json'))
    .map((entry) => path.join(mapsDir, entry.name))
    .sort((left, right) => path.basename(left).localeCompare(path.basename(right)));
}

function loadJobs(mapsDir) {
  const jobsDir = getJobsDir(mapsDir);
  if (!fs.existsSync(jobsDir)) {
    return [];
  }

  return fs.readdirSync(jobsDir, { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.toLowerCase().endsWith('.json'))
    .map((entry) => {
      const filePath = path.join(jobsDir, entry.name);
      const raw = fs.readFileSync(filePath, 'utf8');
      return {
        filePath,
        job: normalizeLlmJob(JSON.parse(raw), {
          jobId: entry.name.replace(/\.json$/i, ''),
        }),
      };
    })
    .sort((left, right) =>
      left.job.createdAt.localeCompare(right.job.createdAt) ||
      left.job.id.localeCompare(right.job.id));
}

function findJob(mapsDir, jobId, allowedStatuses) {
  const jobs = loadJobs(mapsDir);
  const entry = jobId
    ? jobs.find((item) => item.job.id === jobId)
    : jobs.find((item) => allowedStatuses.includes(item.job.status));

  if (!entry) {
    throw new Error(jobId ? `LLM job "${jobId}" was not found.` : 'No matching LLM job was found.');
  }

  if (!allowedStatuses.includes(entry.job.status)) {
    throw new Error(`LLM job "${entry.job.id}" is ${entry.job.status}; expected ${allowedStatuses.join(' or ')}.`);
  }

  return entry;
}

function saveJobEntry(entry, job) {
  fs.mkdirSync(path.dirname(entry.filePath), { recursive: true });
  fs.writeFileSync(entry.filePath, serializeLlmJob(job), 'utf8');
}

function resolveMapsDir(explicitMapsDir) {
  if (explicitMapsDir) {
    return path.resolve(explicitMapsDir);
  }

  const configPath = path.join(os.homedir(), 'focus-config.json');
  if (fs.existsSync(configPath)) {
    try {
      const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
      if (typeof config.dataFolder === 'string' && config.dataFolder.trim()) {
        return path.join(config.dataFolder, 'FocusMaps');
      }
    } catch {
      // Fall back to the working directory.
    }
  }

  return path.resolve('FocusMaps');
}

function resolveMapPath(mapsDir, mapArg) {
  if (!mapArg) {
    throw new Error('Missing --map.');
  }

  const normalized = normalizeProtocolPath(mapArg);
  if (path.isAbsolute(mapArg) && fs.existsSync(mapArg)) {
    return path.resolve(mapArg);
  }

  const direct = path.resolve(mapsDir, normalized);
  if (fs.existsSync(direct)) {
    return direct;
  }

  const parts = normalized.split('/').filter(Boolean);
  const mapsDirName = path.basename(mapsDir).toLowerCase();
  const focusMapsIndex = parts.findIndex((part) => part.toLowerCase() === mapsDirName);
  if (focusMapsIndex >= 0) {
    const fromFocusMaps = path.resolve(mapsDir, ...parts.slice(focusMapsIndex + 1));
    if (fs.existsSync(fromFocusMaps)) {
      return fromFocusMaps;
    }
  }

  const byFileName = path.resolve(mapsDir, path.basename(normalized));
  if (fs.existsSync(byFileName)) {
    return byFileName;
  }

  throw new Error(`Map "${mapArg}" was not found under "${mapsDir}".`);
}

function protocolPathForMap(mapsDir, filePath) {
  return normalizeProtocolPath(path.relative(mapsDir, filePath));
}

function getJobsDir(mapsDir) {
  return path.join(mapsDir, '_llm', 'jobs');
}

function parseArgs(argv) {
  const options = {};
  const positionals = [];
  for (let index = 0; index < argv.length; index += 1) {
    const token = argv[index];
    if (!token.startsWith('--')) {
      positionals.push(token);
      continue;
    }

    const key = token.slice(2);
    if (key === 'help') {
      options.help = true;
      continue;
    }

    const next = argv[index + 1];
    if (!next || next.startsWith('--')) {
      options[key] = 'true';
      continue;
    }

    options[key] = next;
    index += 1;
  }

  return { options, positionals };
}

function requireOption(options, key) {
  const value = options[key];
  if (typeof value !== 'string' || !value.trim() || value === 'true') {
    throw new Error(`Missing --${key}.`);
  }

  return value;
}

function normalizeProtocolPath(value) {
  return String(value ?? '').replace(/\\/g, '/').replace(/^\/+/, '');
}

function usage() {
  return [
    'Focus LLM interop',
    '',
    'Commands:',
    '  context --map <map.json> --node <nodeId> [--format json|markdown] [--maps-dir <dir>]',
    '  jobs list [--status open|pending|claimed|completed|failed|all] [--format json] [--maps-dir <dir>]',
    '  jobs claim --agent <name> [--job <id>] [--format json|markdown] [--maps-dir <dir>]',
    '  jobs complete --job <id> --answer-file <path> [--agent <name>] [--maps-dir <dir>]',
    '  jobs fail --job <id> --message <text> [--maps-dir <dir>]',
    '',
  ].join('\n');
}

export const testInternals = {
  resolveMapsDir,
  resolveMapPath,
  getJobsDir,
};
