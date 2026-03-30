export type GetFileResult = {
  content: string;
  versionToken: string;
};

export type GitProviderWriteResult = {
  versionToken: string;
  commitSha?: string;
};

export interface GitProvider {
  getFile(path: string): Promise<GetFileResult>;
  putFile(
    path: string,
    content: string,
    versionToken: string | null,
    message: string,
  ): Promise<GitProviderWriteResult>;
  deleteFile?(
    path: string,
    versionToken: string,
    message: string,
  ): Promise<GitProviderWriteResult>;
}
