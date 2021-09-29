﻿using System;
using System.ComponentModel.DataAnnotations;

using Microsoft.EntityFrameworkCore;

using Tgstation.Server.Api.Models.Response;

namespace Tgstation.Server.Host.Models
{
	/// <inheritdoc />
	public sealed class RepositorySettings : Api.Models.Internal.RepositorySettings, IApiTransformable<RepositoryResponse>
	{
		/// <summary>
		/// The row Id.
		/// </summary>
		public long Id { get; set; }

		/// <summary>
		/// The instance <see cref="Api.Models.EntityId.Id"/>.
		/// </summary>
		public long InstanceId { get; set; }

		/// <summary>
		/// See <see cref="Api.Models.Internal.RepositorySettings.UpdateSubmodules"/>.
		/// </summary>
		public new bool UpdateSubmodules
		{
			get => base.UpdateSubmodules ?? throw new InvalidOperationException("UpdateSubmodules was null!");
			set => base.UpdateSubmodules = value;
		}

		/// <summary>
		/// See <see cref="Api.Models.Internal.RepositorySettings.ShowTestMergeCommitters"/>.
		/// </summary>
		public new bool ShowTestMergeCommitters
		{
			get => base.ShowTestMergeCommitters ?? throw new InvalidOperationException("ShowTestMergeCommitters was null!");
			set => base.ShowTestMergeCommitters = value;
		}

		/// <summary>
		/// See <see cref="Api.Models.Internal.RepositorySettings.CreateGitHubDeployments"/>.
		/// </summary>
		public new bool CreateGitHubDeployments
		{
			get => base.CreateGitHubDeployments ?? throw new InvalidOperationException("CreateGitHubDeployments was null!");
			set => base.CreateGitHubDeployments = value;
		}

		/// <summary>
		/// See <see cref="Api.Models.Internal.RepositorySettings.AutoUpdatesKeepTestMerges"/>.
		/// </summary>
		public new bool AutoUpdatesKeepTestMerges
		{
			get => base.AutoUpdatesKeepTestMerges ?? throw new InvalidOperationException("AutoUpdatesKeepTestMerges was null!");
			set => base.AutoUpdatesKeepTestMerges = value;
		}

		/// <summary>
		/// See <see cref="Api.Models.Internal.RepositorySettings.AutoUpdatesSynchronize"/>.
		/// </summary>
		public new bool AutoUpdatesSynchronize
		{
			get => base.AutoUpdatesSynchronize ?? throw new InvalidOperationException("AutoUpdatesSynchronize was null!");
			set => base.AutoUpdatesSynchronize = value;
		}

		/// <summary>
		/// See <see cref="Api.Models.Internal.RepositorySettings.PushTestMergeCommits"/>.
		/// </summary>
		public new bool PushTestMergeCommits
		{
			get => base.PushTestMergeCommits ?? throw new InvalidOperationException("PushTestMergeCommits was null!");
			set => base.PushTestMergeCommits = value;
		}

		/// <summary>
		/// See <see cref="Api.Models.Internal.RepositorySettings.PostTestMergeComment"/>.
		/// </summary>
		public new bool PostTestMergeComment
		{
			get => base.PostTestMergeComment ?? throw new InvalidOperationException("PostTestMergeComment was null!");
			set => base.PostTestMergeComment = value;
		}

		/// <summary>
		/// See <see cref="Api.Models.Internal.RepositorySettings.ShowTestMergeCommitters"/>.
		/// </summary>
		public new string CommitterName
		{
			get => base.CommitterName ?? throw new InvalidOperationException("CommitterName was null!");
			set => base.CommitterName = value;
		}

		/// <summary>
		/// See <see cref="Api.Models.Internal.RepositorySettings.ShowTestMergeCommitters"/>.
		/// </summary>
		public new string CommitterEmail
		{
			get => base.CommitterEmail ?? throw new InvalidOperationException("CommitterEmail was null!");
			set => base.CommitterEmail = value;
		}

		/// <summary>
		/// The parent <see cref="Models.Instance"/>.
		/// </summary>
		[Required]
		[BackingField(nameof(instance))]
		public Instance Instance
		{
			get => instance ?? throw new InvalidOperationException("Instance not set!");
			set => instance = value;
		}

		/// <summary>
		/// Backing field for <see cref="Instance"/>.
		/// </summary>
		Instance? instance;

		/// <inheritdoc />
		public RepositoryResponse ToApi() => new ()
		{
			// AccessToken = AccessToken, // never show this
			AccessUser = AccessUser,
			AutoUpdatesKeepTestMerges = AutoUpdatesKeepTestMerges,
			AutoUpdatesSynchronize = AutoUpdatesSynchronize,
			CommitterEmail = CommitterEmail,
			CommitterName = CommitterName,
			PushTestMergeCommits = PushTestMergeCommits,
			ShowTestMergeCommitters = ShowTestMergeCommitters,
			PostTestMergeComment = PostTestMergeComment,
			CreateGitHubDeployments = CreateGitHubDeployments,
			UpdateSubmodules = UpdateSubmodules,

			// revision information and the rest retrieved by controller
		};
	}
}
