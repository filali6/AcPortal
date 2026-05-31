<#import "template.ftl" as layout>
<@layout.registrationLayout>
    <div class="login-wrapper">
      <div class="login-box">

        <div class="login-left">
          <div class="left-logo">
            
            <span class="left-logo-text">ACPortal</span>
          </div>
          <div class="left-content">
            <p class="left-eyebrow">Nice to see you again</p>
            <h1 class="left-title">Welcome<br>Back</h1>
            <div class="left-divider"></div>
             
          </div>
           
        </div>

        <div class="login-right">
          <h2 class="right-title">Login Account</h2>
          <p class="right-subtitle">Enter your credentials to access your account</p>

          <#if message?has_content>
            <div class="acp-alert acp-alert-${message.type}">
              ${kcSanitize(message.summary)?no_esc}
            </div>
          </#if>

          <form id="kc-form-login" action="${url.loginAction}" method="post">
            <div class="acp-field">
              <div class="acp-input-wrap">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/></svg>
                <input id="username" name="username" type="text"
                  autofocus autocomplete="off"
                  value="${login.username!''}"
                  placeholder="Email ID" />
              </div>
            </div>

            <div class="acp-field">
              <div class="acp-input-wrap">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
                <input id="password" name="password" type="password"
                  autocomplete="off"
                  placeholder="Password" />
              </div>
            </div>

            <div class="acp-bottom-row">
              <#if realm.rememberMe && !usernameEditDisabled??>
              <label class="acp-remember">
                <input type="checkbox" name="rememberMe" <#if login.rememberMe??>checked</#if>>
                Keep me signed in
              </label>
              </#if>
              <#if realm.resetPasswordAllowed>
              <a href="${url.loginResetCredentialsUrl}" class="acp-forgot">Forgot password?</a>
              </#if>
            </div>

            <input type="hidden" name="credentialId"
              <#if auth.selectedCredential?has_content>value="${auth.selectedCredential}"</#if>/>

            <button type="submit" class="acp-btn">SIGN IN</button>
          </form>
        </div>

      </div>
    </div>
</@layout.registrationLayout>