import{d as Q,aj as L,z as P,f as D,x as M,al as E,c as s,af as G,C as R,ad as V,M as X,O as Z,L as $,ae as N,ci as U,a9 as O,P as ee,b as c,o as r,e as l,w as t,l as g,k as i,p as o,n as k,t as f,a as v,$ as W,_ as te}from"./index-B7jvAjdE.js";import{M as ae}from"./MActionModalButton-CvWVuTB-.js";import{a as ne,M as ie}from"./MViewContainer-BTeE2xDT.js";import{M as re}from"./MInputText-CzhsT8yb.js";import{M as oe}from"./MInputTextArea-BMdoSBpm.js";import{M as le}from"./MTabLayout-CxB_BuXt.js";import de from"./MetaRawData-CedjqGjJ.js";import{G as ue}from"./GameConfigActionArchive-Bt_4NQ2E.js";import{G as se}from"./GameConfigActionPublish-R7B0H1nr.js";import{C as ge}from"./CoreUiPlacement-CPf1fwrN.js";import{g as F,b as j}from"./gameConfigs-DlpfRS5r.js";import"./MActionModal-coQLkvWs.js";import"./index-fVjXqxSF.js";import"./MInputHintMessage-DbuBS4Kn.js";import"./debounce-B8gTqdZO.js";import"./isSymbol-B7ZrWRtH.js";import"./MInputSimpleSelectDropdown-DIlBEFcs.js";import"./index-qTA83-by.js";import"./MSingleColumnLayout-B4K0ZAFA.js";import"./utils-Dwlepb3M.js";import"./MInputSwitch-C-Z-JJsG.js";import"./index-B_QkKjtG.js";const fe=Q({__name:"GameConfigDetailView",setup(q,{expose:a}){a();const h=V(),e=L(),A=P(),S=D(),{refresh:C}=M(F()),{data:n,error:u,refresh:m}=M(j(E(e.params.id))),b=s(()=>n.value?.id);G().setDynamicTitle(n,d=>`View ${d.value?.name??"Config"}`);const w=s(()=>n.value?.contents.serverLibraries.PlayerExperiments??0),p=s(()=>{const d=[];if(n.value?.publishBlockingErrors.length&&d.push({title:"Unpublishable Game Config",message:"This game config has errors and can not be published.",variant:"danger",dataTest:"cannot-publish-alert"}),n.value?.status==="Building")d.push({title:"Config Building...",message:"This game config is still building and has no content to view for now.",variant:"warning",dataTest:"building-alert"});else if(n.value?.libraryImportErrors&&Object.keys(n.value.libraryImportErrors).length>0)d.push({title:"Library Errors",message:"One or more libraries failed to import.",variant:"danger",dataTest:"libraries-fail-to-parse-alert"});else if(n.value?.buildReportSummary?.totalLogLevelCounts.Warning){const x=n.value.buildReportSummary.totalLogLevelCounts.Warning??0;d.push({title:`${n.value.buildReportSummary.totalLogLevelCounts.Warning} Build Warnings`,message:`There ${U(x,"was","were")} ${O(x,"warning")} when building this config.
        You can still publish it, but it may not work as expected. You can view the full build log for more information.`,variant:"warning",dataTest:"build-warnings-alert"})}return d}),y=s(()=>p.value.find(d=>d.variant==="danger")?"danger":p.value.find(d=>d.variant==="warning")?"warning":void 0),_=D({name:"",description:"",isArchived:!1});function z(){_.value={name:n.value?.name??"",description:n.value?.description??"",isArchived:n.value?.isArchived??!1}}const{showSuccessNotification:B}=N();async function H(){const d={name:_.value.name,description:_.value.description,isArchived:_.value.isArchived};await h.post(`/gameConfig/${b.value}`,d),B("Game config updated."),C(),m()}const K=s(()=>n.value?.isActive?"Cannot diff this config against itself.":n.value?.status==="Building"?"Cannot diff a config while it's being built":n.value?.fullConfigVersion?void 0:"Cannot diff a config that failed to build."),Y=s(()=>!n.value?.isActive),T=D(0);R(n,d=>{if(d&&d.publishBlockingErrors.length>0&&!e.query.tab){for(const x of d.publishBlockingErrors)if(x.errorType==="BlockingMessages"){T.value=1;break}}},{immediate:!0});const J=s(()=>[{label:"Details"},{label:"Build Log",highlighted:n.value&&n.value.publishBlockingErrors.length>0},{label:"Audit Log"}]),I={gameServerApi:h,route:e,coreStore:A,gameConfigArchiveModalRef:S,allGameConfigsRefresh:C,gameConfigData:n,gameConfigError:u,gameConfigRefresh:m,gameConfigId:b,totalExperiments:w,alerts:p,errorVariant:y,editModalConfig:_,resetForm:z,showSuccessNotification:B,sendUpdatedConfigDataToServer:H,disallowDiffToActiveReason:K,canArchive:Y,currentTab:T,tabOptions:J,tabUiPlacements:["GameConfigs/Details/Tab0","GameConfigs/Details/Tab1","GameConfigs/Details/Tab2"],computed:s,ref:D,watch:R,get useRoute(){return L},get useGameServerApi(){return V},get MetaRawData(){return de},get MActionModalButton(){return ae},get MBadge(){return X},get MButton(){return Z},get MInputText(){return re},get MInputTextArea(){return oe},get MTooltip(){return $},get useHeaderbar(){return G},get MViewContainer(){return ne},get MPageOverviewCard(){return ie},get useNotifications(){return N},get MTabLayout(){return le},get maybePluralPrefixString(){return U},get maybePluralString(){return O},get sentenceCaseToKebabCase(){return ee},get useSubscription(){return M},GameConfigActionArchive:ue,GameConfigActionPublish:se,CoreUiPlacement:ge,get useCoreStore(){return P},get routeParamToSingleValue(){return E},get getAllGameConfigsSubscriptionOptions(){return F},get getSingleGameConfigCountsSubscriptionOptions(){return j}};return Object.defineProperty(I,"__isScriptSetup",{enumerable:!1,value:!0}),I}}),me={class:"font-weight-bold"},ce={key:1,class:"text-muted tw-italic"},Ce={key:2,class:"text-muted tw-italic"},be={key:1,class:"text-muted tw-italic"},ve={class:"font-weight-bold"},we={class:"font-weight-bold"},pe={key:0,class:"text-monospace small"},ye={key:1,class:"text-muted tw-italic"},_e={key:0,class:"text-monospace small"};function xe(q,a,h,e,A,S){const C=c("fa-icon"),n=c("b-td"),u=c("b-tr"),m=c("meta-time"),b=c("b-tbody"),w=c("b-table-simple"),p=c("meta-username");return r(),l(e.MViewContainer,{variant:e.errorVariant,"is-loading":!e.gameConfigData,error:e.gameConfigError,"full-width":"",alerts:e.alerts,permission:"api.game_config.view"},{overview:t(()=>[e.gameConfigData?(r(),l(e.MPageOverviewCard,{key:0,title:e.gameConfigData.name,subtitle:e.gameConfigData.description,id:e.gameConfigId,"data-testid":"game-config-detail-overview-card"},{buttons:t(()=>[e.gameConfigId?(r(),l(e.GameConfigActionArchive,{key:0,gameConfigId:e.gameConfigId,"trigger-style":"button"},null,8,["gameConfigId"])):g("",!0),i(e.MActionModalButton,{"modal-title":"Edit Game Config Archive",action:e.sendUpdatedConfigDataToServer,"trigger-button-label":"Edit","ok-button-label":"Update",permission:"api.game_config.edit","trigger-button-full-width":"",onShow:e.resetForm,"data-testid":"edit-config"},{default:t(()=>[i(e.MInputText,{class:"tw-mb-2",label:"Name","model-value":e.editModalConfig.name,variant:e.editModalConfig.name.length>0?"success":"default",placeholder:"For example: 1.0.4 release candidate","onUpdate:modelValue":a[0]||(a[0]=y=>e.editModalConfig.name=y)},null,8,["model-value","variant"]),i(e.MInputTextArea,{label:"Description","model-value":e.editModalConfig.description,variant:e.editModalConfig.description.length>0?"success":"default",placeholder:"What is unique about this config build that will help you find it later?",rows:3,"onUpdate:modelValue":a[1]||(a[1]=y=>e.editModalConfig.description=y)},null,8,["model-value","variant"])],void 0,!0),_:1}),i(e.MButton,{"disabled-tooltip":e.disallowDiffToActiveReason,to:`diff?newRoot=${e.gameConfigId}`,"full-width":""},{default:t(()=>a[30]||(a[30]=[o("Diff to Active")]),void 0,!0),_:1},8,["disabled-tooltip","to"]),e.gameConfigId?(r(),l(e.GameConfigActionPublish,{key:1,gameConfigId:e.gameConfigId,publishBlocked:e.gameConfigData?.publishBlockingErrors.length>0},null,8,["gameConfigId","publishBlocked"])):g("",!0)]),default:t(()=>[k("span",me,[i(C,{icon:"chart-bar"}),a[2]||(a[2]=o(" Overview"))]),i(w,{small:"",responsive:""},{default:t(()=>[i(b,null,{default:t(()=>[i(u,null,{default:t(()=>[i(n,null,{default:t(()=>a[3]||(a[3]=[o("Status")]),void 0,!0),_:1}),i(n,{class:"tw-text-right"},{default:t(()=>[e.gameConfigData.isArchived?(r(),l(e.MBadge,{key:0,class:"tw-mr-1",variant:"neutral"},{default:t(()=>a[4]||(a[4]=[o("Archived")]),void 0,!0),_:1})):g("",!0),e.gameConfigData.isActive?(r(),l(e.MBadge,{key:1,class:"tw-mr-1",variant:"success"},{default:t(()=>a[5]||(a[5]=[o("Active")]),void 0,!0),_:1})):(r(),l(e.MBadge,{key:2},{default:t(()=>a[6]||(a[6]=[o("Not active")]),void 0,!0),_:1}))],void 0,!0),_:1})],void 0,!0),_:1}),i(u,null,{default:t(()=>[i(n,null,{default:t(()=>a[7]||(a[7]=[o("Experiments")]),void 0,!0),_:1}),e.totalExperiments===void 0?(r(),l(n,{key:0,class:"text-right text-muted tw-italic"},{default:t(()=>a[8]||(a[8]=[o("Loading...")]),void 0,!0),_:1})):e.totalExperiments>0?(r(),l(n,{key:1,class:"tw-text-right"},{default:t(()=>[o(f(e.totalExperiments),1)],void 0,!0),_:1})):(r(),l(n,{key:2,class:"text-right text-muted tw-italic"},{default:t(()=>a[9]||(a[9]=[o("None")]),void 0,!0),_:1}))],void 0,!0),_:1}),i(u,null,{default:t(()=>[i(n,null,{default:t(()=>a[10]||(a[10]=[o("Last Published At")]),void 0,!0),_:1}),i(n,{class:"tw-text-right"},{default:t(()=>[e.gameConfigData?.publishedAt?(r(),l(m,{key:0,date:e.gameConfigData?.publishedAt,showAs:"timeagoSentenceCase"},null,8,["date"])):e.gameConfigData?.isActive?(r(),v("span",ce,"No time recorded")):(r(),v("span",Ce,"Never"))],void 0,!0),_:1})],void 0,!0),_:1}),i(u,null,{default:t(()=>[i(n,null,{default:t(()=>a[11]||(a[11]=[o("Last Unpublished At")]),void 0,!0),_:1}),i(n,{class:"tw-text-right"},{default:t(()=>[e.gameConfigData?.unpublishedAt?(r(),l(m,{key:0,date:e.gameConfigData?.unpublishedAt,showAs:"timeagoSentenceCase"},null,8,["date"])):(r(),v("span",be,"Never"))],void 0,!0),_:1})],void 0,!0),_:1})],void 0,!0),_:1})],void 0,!0),_:1}),k("span",ve,[i(C,{icon:"chart-bar"}),a[12]||(a[12]=o(" Build Status"))]),i(w,{small:"",responsive:""},{default:t(()=>[i(b,null,{default:t(()=>[i(u,null,{default:t(()=>[i(n,null,{default:t(()=>a[13]||(a[13]=[o("Build Status")]),void 0,!0),_:1}),i(n,{class:"tw-text-right"},{default:t(()=>[e.gameConfigData?.status==="Success"?(r(),l(e.MBadge,{key:0,variant:"success"},{default:t(()=>[o(f(e.gameConfigData?.status),1)],void 0,!0),_:1})):g("",!0),e.gameConfigData?.status==="Failed"?(r(),l(e.MBadge,{key:1,variant:"danger"},{default:t(()=>[o(f(e.gameConfigData?.status),1)],void 0,!0),_:1})):g("",!0),e.gameConfigData?.status==="Building"?(r(),l(e.MBadge,{key:2,variant:"primary"},{default:t(()=>[o(f(e.gameConfigData?.status),1)],void 0,!0),_:1})):g("",!0)],void 0,!0),_:1})],void 0,!0),_:1}),i(u,null,{default:t(()=>[i(n,null,{default:t(()=>a[14]||(a[14]=[o("Built By")]),void 0,!0),_:1}),i(n,{class:"tw-text-right"},{default:t(()=>[e.gameConfigData.source==="disk"?(r(),l(e.MBadge,{key:0},{default:t(()=>a[15]||(a[15]=[o("Built-in with the server")]),void 0,!0),_:1})):(r(),l(p,{key:1,username:e.gameConfigData.source},null,8,["username"]))],void 0,!0),_:1})],void 0,!0),_:1}),i(u,{class:W({"text-danger":e.gameConfigData.buildReportSummary?.totalLogLevelCounts.Error})},{default:t(()=>[i(n,null,{default:t(()=>a[16]||(a[16]=[o("Logged Errors")]),void 0,!0),_:1}),e.gameConfigData?.buildReportSummary?.totalLogLevelCounts.Error?(r(),l(n,{key:0,class:"tw-text-right"},{default:t(()=>[o(f(e.gameConfigData.buildReportSummary?.totalLogLevelCounts.Error),1)],void 0,!0),_:1})):e.gameConfigData?.buildReportSummary===null?(r(),l(n,{key:1,class:"text-right text-muted tw-italic"},{default:t(()=>a[17]||(a[17]=[o("Not available")]),void 0,!0),_:1})):(r(),l(n,{key:2,class:"text-right text-muted tw-italic"},{default:t(()=>a[18]||(a[18]=[o("None")]),void 0,!0),_:1}))],void 0,!0),_:1},8,["class"]),i(u,{class:W({"text-warning":e.gameConfigData?.buildReportSummary?.totalLogLevelCounts.Warning})},{default:t(()=>[i(n,null,{default:t(()=>a[19]||(a[19]=[o("Logged Warnings")]),void 0,!0),_:1}),e.gameConfigData?.buildReportSummary?.totalLogLevelCounts.Warning?(r(),l(n,{key:0,class:"tw-text-right"},{default:t(()=>[o(f(e.gameConfigData.buildReportSummary?.totalLogLevelCounts.Warning),1)],void 0,!0),_:1})):e.gameConfigData?.buildReportSummary===null?(r(),l(n,{key:1,class:"text-right text-muted tw-italic"},{default:t(()=>a[20]||(a[20]=[o("Not available")]),void 0,!0),_:1})):(r(),l(n,{key:2,class:"text-right text-muted tw-italic"},{default:t(()=>a[21]||(a[21]=[o("None")]),void 0,!0),_:1}))],void 0,!0),_:1},8,["class"])],void 0,!0),_:1})],void 0,!0),_:1}),k("span",we,[i(C,{icon:"chart-bar"}),a[22]||(a[22]=o(" Technical Details"))]),i(w,{small:"",responsive:""},{default:t(()=>[i(b,null,{default:t(()=>[i(u,null,{default:t(()=>[i(n,null,{default:t(()=>a[23]||(a[23]=[o("Built At")]),void 0,!0),_:1}),i(n,{class:"tw-text-right"},{default:t(()=>[i(m,{date:e.gameConfigData?.buildStartedAt,showAs:"timeagoSentenceCase"},null,8,["date"])],void 0,!0),_:1})],void 0,!0),_:1}),i(u,null,{default:t(()=>[i(n,null,{default:t(()=>a[24]||(a[24]=[o("Last Modified At")]),void 0,!0),_:1}),i(n,{class:"tw-text-right"},{default:t(()=>[e.gameConfigData?(r(),l(m,{key:0,date:e.gameConfigData?.lastModifiedAt,showAs:"timeagoSentenceCase"},null,8,["date"])):g("",!0)],void 0,!0),_:1})],void 0,!0),_:1}),i(u,null,{default:t(()=>[i(n,null,{default:t(()=>a[25]||(a[25]=[o("Full Config Archive Version")]),void 0,!0),_:1}),e.gameConfigData?(r(),l(n,{key:1,class:"tw-text-right"},{default:t(()=>[e.gameConfigData?.fullConfigVersion?(r(),v("div",pe,f(e.gameConfigData.fullConfigVersion),1)):(r(),v("div",ye,"Not available"))],void 0,!0),_:1})):(r(),l(n,{key:0,class:"text-right text-muted tw-italic"},{default:t(()=>a[26]||(a[26]=[o("Loading...")]),void 0,!0),_:1}))],void 0,!0),_:1}),i(u,null,{default:t(()=>[i(n,null,{default:t(()=>a[27]||(a[27]=[o("Client Facing Version")]),void 0,!0),_:1}),e.gameConfigData?(r(),l(n,{key:1,class:"tw-text-right"},{default:t(()=>[e.gameConfigData?.cdnVersion?(r(),v("div",_e,f(e.gameConfigData.cdnVersion),1)):(r(),l(e.MTooltip,{key:1,class:"text-muted tw-italic",content:"Only available for the currently active game config."},{default:t(()=>a[29]||(a[29]=[o("Not available")]),void 0,!0),_:1}))],void 0,!0),_:1})):(r(),l(n,{key:0,class:"text-right text-muted tw-italic"},{default:t(()=>a[28]||(a[28]=[o("Loading...")]),void 0,!0),_:1}))],void 0,!0),_:1})],void 0,!0),_:1})],void 0,!0),_:1})],void 0,!0),_:1},8,["title","subtitle","id"])):g("",!0)]),default:t(()=>[i(e.MTabLayout,{tabs:e.tabOptions,"current-tab":e.currentTab},{"tab-0":t(()=>[i(e.CoreUiPlacement,{placementId:e.tabUiPlacements[0],gameConfigId:e.gameConfigId,alwaysFullWidth:""},null,8,["placementId","gameConfigId"])]),"tab-1":t(()=>[i(e.CoreUiPlacement,{placementId:e.tabUiPlacements[1],gameConfigId:e.gameConfigId},null,8,["placementId","gameConfigId"])]),"tab-2":t(()=>[i(e.CoreUiPlacement,{placementId:e.tabUiPlacements[2],gameConfigId:e.gameConfigId},null,8,["placementId","gameConfigId"])]),_:1},8,["tabs","current-tab"]),i(e.MetaRawData,{kvPair:e.gameConfigData,name:"gameConfigData"},null,8,["kvPair"])]),_:1},8,["variant","is-loading","error","alerts"])}const He=te(fe,[["render",xe],["__file","GameConfigDetailView.vue"]]);export{He as default};
