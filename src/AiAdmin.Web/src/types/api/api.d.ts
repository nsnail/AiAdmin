/**
 * API 接口类型定义模块
 *
 * 提供所有后端接口的类型定义
 *
 * ## 主要功能
 *
 * - 通用类型（分页参数、响应结构等）
 * - 认证类型（登录、用户信息等）
 * - 系统管理类型（用户、角色等）
 * - 全局命名空间声明
 *
 * ## 使用场景
 *
 * - API 请求参数类型约束
 * - API 响应数据类型定义
 * - 接口文档类型同步
 *
 * ## 注意事项
 *
 * - 在 .vue 文件使用需要在 eslint.config.mjs 中配置 globals: { Api: 'readonly' }
 * - 使用全局命名空间，无需导入即可使用
 *
 * ## 使用方式
 *
 * ```typescript
 * const params: Api.Auth.LoginParams = { userName: 'admin', password: '123456' }
 * const response: Api.Auth.UserInfo = await fetchUserInfo()
 * ```
 *
 * @module types/api/api
 * @author Art Design Pro Team
 */

declare namespace Api {
  /** 通用类型 */
  namespace Common {
    /** 分页参数 */
    interface PaginationParams {
      /** 当前页码 */
      current: number
      /** 每页条数 */
      size: number
      /** 总条数 */
      total: number
    }

    /** 通用搜索参数 */
    type CommonSearchParams = Pick<PaginationParams, 'current' | 'size'>

    /** 分页响应基础结构 */
    interface PaginatedResponse<T = any> {
      records: T[]
      current: number
      size: number
      total: number
    }

    /** 启用状态 */
    type EnableStatus = '1' | '2'
  }

  /** 认证类型 */
  namespace Auth {
    /** 登录参数 */
    interface LoginParams {
      userName: string
      password: string
      challenge: string
      proof: string
    }

    interface RegisterParams {
      userName: string
      password: string
      email: string
      verificationCode: string
      invitationCode?: string
    }

    /** 登录响应 */
    interface LoginResponse {
      token: string
      refreshToken: string
    }

    interface LoginConfig {
      loginSliderVerification: boolean
      registrationEnabled: boolean
      emailVerificationEnabled: boolean
    }

    interface LoginChallenge {
      challenge: string
      difficulty: number
    }

    interface RegisterPuzzle {
      challengeId: string
      backgroundImage: string
      pieceImage: string
      width: number
      height: number
      pieceSize: number
      pieceY: number
    }

    interface VerifyRegisterPuzzleResult {
      puzzleTicket: string
    }

    /** 用户信息 */
    interface UserInfo {
      buttons: string[]
      roles: string[]
      userId: string
      userName: string
      email: string
      phone: string
      gender: string
      avatar?: string
    }

    /** 当前用户资料更新参数 */
    interface UpdateProfileParams {
      email: string
      phone: string
      gender: string
    }

    /** 当前用户密码修改参数 */
    interface ChangePasswordParams {
      currentPassword: string
      newPassword: string
    }
  }

  /** 系统管理类型 */
  namespace SystemManage {
    /** 用户列表 */
    type UserList = Api.Common.PaginatedResponse<UserListItem>

    /** 用户列表项 */
    interface UserListItem {
      id: string
      avatar: string
      status: string
      userName: string
      userGender: string
      userPhone: string
      userEmail: string
      userRoles: string[]
      departmentIds: string[]
      departmentNames: string[]
      createBy: string
      createTime: string
      updateBy: string
      updateTime: string | null
    }

    /** 用户搜索参数 */
    type UserSearchParams = Partial<
      Pick<UserListItem, 'id' | 'userName' | 'userGender' | 'userPhone' | 'userEmail' | 'status'> &
        Api.Common.CommonSearchParams
    > & { dynamicFilter?: import('@/api/system-manage').DynamicFilter }

    interface SaveUserParams {
      userName: string
      email: string
      phone: string
      gender: string
      roles: string[]
      departmentIds: string[]
      password?: string
      isEnabled: boolean
    }

    /** 当前用户邀请关系查询结果 */
    interface ReferralTreeResult {
      invitationCode: string
      children: ReferralTreeItem[]
    }

    /** 邀请关系树节点 */
    interface ReferralTreeItem {
      id: string
      userName: string
      email: string
      invitationCode: string
      createdAt: string
      children: ReferralTreeItem[]
    }

    /** 部门树节点 */
    interface DepartmentTreeItem {
      id: string
      name: string
      code: string
      parentId: string | null
      sort: number
      leader: string
      phone: string
      email: string
      isEnabled: boolean
      createdAt: string
      children: DepartmentTreeItem[]
    }

    /** 部门保存参数 */
    type SaveDepartmentParams = Pick<
      DepartmentTreeItem,
      'name' | 'code' | 'parentId' | 'sort' | 'leader' | 'phone' | 'email' | 'isEnabled'
    >

    /** 角色列表 */
    type RoleList = Api.Common.PaginatedResponse<RoleListItem>

    /** 角色列表项 */
    interface RoleListItem {
      roleId: string
      roleName: string
      roleCode: string
      description: string
      dataScope: 'all' | 'department' | 'department_and_children' | 'self'
      enabled: boolean
      createTime: string
    }

    type SaveRoleParams = Pick<
      RoleListItem,
      'roleName' | 'roleCode' | 'description' | 'dataScope' | 'enabled'
    >

    interface ApiEndpointItem {
      id: string
      name: string
      allowAnonymous: boolean
      method: string
      path: string
      controller: string
      controllerName: string
      action: string
    }

    interface DictionaryCategory {
      id: string
      code: string
      name: string
      parentId: string | null
      sort: number
      isEnabled: boolean
      children: DictionaryCategory[]
    }

    interface DictionaryItem {
      id: string
      categoryId: string
      value: string
      label: string
      sort: number
      isEnabled: boolean
      remark: string
    }

    type SaveDictionaryCategoryParams = Pick<
      DictionaryCategory,
      'code' | 'name' | 'parentId' | 'sort' | 'isEnabled'
    >

    type SaveDictionaryItemParams = Pick<
      DictionaryItem,
      'value' | 'label' | 'sort' | 'isEnabled' | 'remark'
    >

    interface ApiSyncResult {
      added: number
      updated: number
      deleted: number
      total: number
    }

    /** 角色搜索参数 */
    type RoleSearchParams = Partial<
      Pick<RoleListItem, 'roleId' | 'roleName' | 'roleCode' | 'description' | 'enabled'> &
        Api.Common.CommonSearchParams & {
          startTime: string | null
          endTime: string | null
        }
    > & { dynamicFilter?: import('@/api/system-manage').DynamicFilter }
  }
}
