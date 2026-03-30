import { mapAuthFailure } from './errors.js';
import { GitHubAdapter } from '../gitProvider/adapters/githubAdapter.js';

export async function validateToken(token, probeOptions) {
  try {
    const adapter = new GitHubAdapter({
      owner: probeOptions.repoOwner,
      repo: probeOptions.repoName,
      branch: probeOptions.repoBranch,
      token,
    });
    await adapter.probeRepository();
    return { ok: true };
  } catch (error) {
    return { ok: false, error: mapAuthFailure(error) };
  }
}
