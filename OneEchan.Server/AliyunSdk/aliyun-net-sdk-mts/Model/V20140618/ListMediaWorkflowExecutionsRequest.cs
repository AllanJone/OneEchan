/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using Aliyun.Acs.Core;
using Aliyun.Acs.Core.Transform;
using Aliyun.Acs.Core.Utils;
using Aliyun.Acs.Mts.Transform.V20140618;

namespace Aliyun.Acs.Mts.Model.V20140618
{
    public class ListMediaWorkflowExecutionsRequest : RpcAcsRequest<ListMediaWorkflowExecutionsResponse>
    {
        private string inputFileURL;

        private long? maximumPageSize;

        private string mediaWorkflowId;

        private string mediaWorkflowName;

        private string nextPageToken;

        private string ownerAccount;

        private long? ownerId;

        private string resourceOwnerAccount;

        private long? resourceOwnerId;

        public ListMediaWorkflowExecutionsRequest()
            : base("Mts", "2014-06-18", "ListMediaWorkflowExecutions")
        {
        }

        public long? OwnerId
        {
            get => ownerId;
            set
            {
                ownerId = value;
                DictionaryUtil.Add(QueryParameters, "OwnerId", value.ToString());
            }
        }

        public string ResourceOwnerAccount
        {
            get => resourceOwnerAccount;
            set
            {
                resourceOwnerAccount = value;
                DictionaryUtil.Add(QueryParameters, "ResourceOwnerAccount", value);
            }
        }

        public long? ResourceOwnerId
        {
            get => resourceOwnerId;
            set
            {
                resourceOwnerId = value;
                DictionaryUtil.Add(QueryParameters, "ResourceOwnerId", value.ToString());
            }
        }

        public string MediaWorkflowId
        {
            get => mediaWorkflowId;
            set
            {
                mediaWorkflowId = value;
                DictionaryUtil.Add(QueryParameters, "MediaWorkflowId", value);
            }
        }

        public string MediaWorkflowName
        {
            get => mediaWorkflowName;
            set
            {
                mediaWorkflowName = value;
                DictionaryUtil.Add(QueryParameters, "MediaWorkflowName", value);
            }
        }

        public string InputFileURL
        {
            get => inputFileURL;
            set
            {
                inputFileURL = value;
                DictionaryUtil.Add(QueryParameters, "InputFileURL", value);
            }
        }

        public string NextPageToken
        {
            get => nextPageToken;
            set
            {
                nextPageToken = value;
                DictionaryUtil.Add(QueryParameters, "NextPageToken", value);
            }
        }

        public long? MaximumPageSize
        {
            get => maximumPageSize;
            set
            {
                maximumPageSize = value;
                DictionaryUtil.Add(QueryParameters, "MaximumPageSize", value.ToString());
            }
        }

        public string OwnerAccount
        {
            get => ownerAccount;
            set
            {
                ownerAccount = value;
                DictionaryUtil.Add(QueryParameters, "OwnerAccount", value);
            }
        }

        public override ListMediaWorkflowExecutionsResponse GetResponse(UnmarshallerContext unmarshallerContext)
        {
            return ListMediaWorkflowExecutionsResponseUnmarshaller.Unmarshall(unmarshallerContext);
        }
    }
}